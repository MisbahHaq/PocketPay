using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class LedgerImmutabilityTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Update_on_ledger_entries_is_rejected_by_the_database()
    {
        await using var scenario = await LedgerScenario.CreateAsync(environment);
        await scenario.PostFundingAsync(100_000);

        var act = async () =>
        {
            await using var command = scenario.DbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "UPDATE \"LedgerEntries\" SET \"AmountMinorUnits\" = 1;";
            await scenario.DbContext.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public async Task Delete_on_ledger_entries_is_rejected_by_the_database()
    {
        await using var scenario = await LedgerScenario.CreateAsync(environment);
        await scenario.PostFundingAsync(100_000);

        var act = async () =>
        {
            await using var command = scenario.DbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "DELETE FROM \"LedgerEntries\";";
            await scenario.DbContext.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public async Task Update_on_journal_entries_is_rejected_by_the_database()
    {
        await using var scenario = await LedgerScenario.CreateAsync(environment);
        await scenario.PostFundingAsync(100_000);

        var act = async () =>
        {
            await using var command = scenario.DbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "UPDATE \"JournalEntries\" SET \"Description\" = 'tampered';";
            await scenario.DbContext.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*append-only*");
    }

    /// <summary>
    /// A disposable test helper that owns a DI scope, a DbContext, and a couple of accounts
    /// so immutability tests can post a real journal entry first.
    /// </summary>
    private sealed class LedgerScenario(
        IServiceScope scope,
        WalletDbContext dbContext,
        LedgerAccount funding,
        LedgerAccount wallet) : IAsyncDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public WalletDbContext DbContext { get; } = dbContext;

        public static async Task<LedgerScenario> CreateAsync(ApiEnvironment environment)
        {
            var scope = environment.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

            var user = User.Create($"ledger+{Guid.NewGuid():N}@example.com", "hash", Role.Customer, Now);
            var wallet = Wallet.Create(user.Id, Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(), Currency.PKR, Now);
            dbContext.Users.Add(user);
            dbContext.Wallets.Add(wallet);

            var funding = await dbContext.LedgerAccounts
                .FirstOrDefaultAsync(a => a.SystemCode == SystemAccount.Funding && a.Currency == Currency.PKR);

            if (funding is null)
            {
                funding = LedgerAccount.CreateSystem(SystemAccount.Funding, Currency.PKR, Now);
                dbContext.LedgerAccounts.Add(funding);
            }

            var walletAccount = LedgerAccount.CreateForWallet(wallet.Id, Currency.PKR, Now);
            dbContext.LedgerAccounts.Add(walletAccount);
            await dbContext.SaveChangesAsync();

            return new LedgerScenario(scope, dbContext, funding, walletAccount);
        }

        public async Task PostFundingAsync(long amountMinorUnits)
        {
            var entry = JournalEntry.Create("Funding", "test", Now)
                .Debit(funding, new Money(amountMinorUnits, Currency.PKR))
                .Credit(wallet, new Money(amountMinorUnits, Currency.PKR))
                .Build();

            DbContext.JournalEntries.Add(entry);
            await DbContext.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            scope.Dispose();
        }
    }
}
