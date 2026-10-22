using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class LedgerServiceTests(ApiEnvironment environment)
{
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Wallet_balance_is_derived_from_postings()
    {
        using var scope = environment.CreateScope();
        var (db, ledger) = Resolve(scope);
        var (funding, walletAccount) = await CreateAccountsAsync(db);

        await ledger.PostAsync(Funding(funding, walletAccount, 100_000));
        await ledger.PostAsync(Funding(funding, walletAccount, 25_000));

        var balance = await ledger.GetBalanceAsync(walletAccount.Id);

        // A customer wallet is a liability (credit-normal): credits increase the balance.
        balance.Should().Be(125_000);
    }

    [Fact]
    public async Task System_funding_balance_is_debit_normal()
    {
        using var scope = environment.CreateScope();
        var (db, ledger) = Resolve(scope);
        var (funding, walletAccount) = await CreateAccountsAsync(db);

        // The seeded funding account is shared across tests, so assert on the change
        // rather than an absolute value.
        var before = await ledger.GetBalanceAsync(funding.Id) ?? 0;

        await ledger.PostAsync(Funding(funding, walletAccount, 100_000));

        var after = await ledger.GetBalanceAsync(funding.Id);

        // Funding is an asset (debit-normal): debits increase the balance.
        (after - before).Should().Be(100_000);
    }

    [Fact]
    public async Task Balance_of_unknown_account_is_null()
    {
        using var scope = environment.CreateScope();
        var (_, ledger) = Resolve(scope);

        var balance = await ledger.GetBalanceAsync(Guid.NewGuid());

        balance.Should().BeNull();
    }

    [Fact]
    public async Task Statement_shows_running_balance_oldest_first()
    {
        using var scope = environment.CreateScope();
        var (db, ledger) = Resolve(scope);
        var (funding, walletAccount) = await CreateAccountsAsync(db);

        await ledger.PostAsync(Funding(funding, walletAccount, 100_000, Now));
        await ledger.PostAsync(Funding(funding, walletAccount, 25_000, Now.AddMinutes(1)));
        await ledger.PostAsync(Funding(funding, walletAccount, 5_000, Now.AddMinutes(2)));

        var statement = await ledger.GetStatementAsync(walletAccount.Id, page: 1, pageSize: 10);

        statement.TotalCount.Should().Be(3);
        statement.ClosingBalanceMinorUnits.Should().Be(130_000);
        statement.Lines.Select(line => line.RunningBalanceMinorUnits)
            .Should().Equal(100_000, 125_000, 130_000);
        statement.Lines.Should().OnlyContain(line => line.Direction == Direction.Credit);
    }

    [Fact]
    public async Task Statement_paginates_but_closing_balance_covers_all_postings()
    {
        using var scope = environment.CreateScope();
        var (db, ledger) = Resolve(scope);
        var (funding, walletAccount) = await CreateAccountsAsync(db);

        for (var i = 1; i <= 5; i++)
        {
            await ledger.PostAsync(Funding(funding, walletAccount, 10_000, Now.AddMinutes(i)));
        }

        var page2 = await ledger.GetStatementAsync(walletAccount.Id, page: 2, pageSize: 2);

        page2.TotalCount.Should().Be(5);
        page2.Lines.Should().HaveCount(2);
        page2.Lines[0].RunningBalanceMinorUnits.Should().Be(30_000);
        page2.Lines[1].RunningBalanceMinorUnits.Should().Be(40_000);
        page2.ClosingBalanceMinorUnits.Should().Be(50_000);
    }

    [Fact]
    public async Task Statement_for_unknown_account_throws_not_found()
    {
        using var scope = environment.CreateScope();
        var (_, ledger) = Resolve(scope);

        var act = () => ledger.GetStatementAsync(Guid.NewGuid(), page: 1, pageSize: 10);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static (WalletDbContext Db, ILedgerService Ledger) Resolve(IServiceScope scope) =>
        (scope.ServiceProvider.GetRequiredService<WalletDbContext>(),
         scope.ServiceProvider.GetRequiredService<ILedgerService>());

    private async Task<(LedgerAccount Funding, LedgerAccount Wallet)> CreateAccountsAsync(WalletDbContext db)
    {
        var user = User.Create($"ledger+{Guid.NewGuid():N}@example.com", "hash", Role.Customer, Now);
        var wallet = Wallet.Create(user.Id, Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(), Currency.PKR, Now);
        db.Users.Add(user);
        db.Wallets.Add(wallet);

        var funding = await db.LedgerAccounts
            .FirstOrDefaultAsync(a => a.SystemCode == SystemAccount.Funding && a.Currency == Currency.PKR);

        if (funding is null)
        {
            funding = LedgerAccount.CreateSystem(SystemAccount.Funding, Currency.PKR, Now);
            db.LedgerAccounts.Add(funding);
        }

        var walletAccount = LedgerAccount.CreateForWallet(wallet.Id, Currency.PKR, Now);
        db.LedgerAccounts.Add(walletAccount);
        await db.SaveChangesAsync();

        return (funding, walletAccount);
    }

    private static JournalEntry Funding(
        LedgerAccount funding,
        LedgerAccount wallet,
        long amountMinorUnits,
        DateTimeOffset? createdAtUtc = null) =>
        JournalEntry.Create("Funding", "test", createdAtUtc ?? Now)
            .Debit(funding, new Money(amountMinorUnits, Currency.PKR))
            .Credit(wallet, new Money(amountMinorUnits, Currency.PKR))
            .Build();
}
