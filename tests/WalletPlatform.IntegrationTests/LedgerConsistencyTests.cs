using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class LedgerConsistencyTests(ApiEnvironment environment)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Posting_an_entry_with_a_missing_account_leaves_no_partial_rows()
    {
        using var scope = environment.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();

        var funding = await GetFundingAsync(db);
        var ghost = LedgerAccount.CreateForWallet(Guid.NewGuid(), Currency.PKR, Now);

        // A syntactically valid, balanced entry whose second account was never persisted.
        // The insert will violate the LedgerEntries -> LedgerAccounts foreign key.
        var entry = JournalEntry.Create("Funding", "test", Now)
            .Debit(funding, new Money(50_000, Currency.PKR))
            .Credit(ghost, new Money(50_000, Currency.PKR))
            .Build();

        var act = () => ledger.PostAsync(entry);

        await act.Should().ThrowAsync<DbUpdateException>();

        // Neither the parent journal entry nor any posting may survive a failed insert.
        (await db.JournalEntries.CountAsync(e => e.Id == entry.Id)).Should().Be(0);
        (await db.LedgerEntries.CountAsync(e => e.JournalEntryId == entry.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Failed_posting_does_not_affect_existing_balances()
    {
        using var scope = environment.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();

        var (funding, walletAccount) = await CreateWalletAccountAsync(db);

        await ledger.PostAsync(Funding(funding, walletAccount, 100_000));
        var before = await ledger.GetBalanceAsync(walletAccount.Id);

        var ghost = LedgerAccount.CreateForWallet(Guid.NewGuid(), Currency.PKR, Now);
        var broken = JournalEntry.Create("Funding", "test", Now)
            .Debit(funding, new Money(70_000, Currency.PKR))
            .Credit(ghost, new Money(70_000, Currency.PKR))
            .Build();

        await FluentActions.Awaiting(() => ledger.PostAsync(broken))
            .Should().ThrowAsync<DbUpdateException>();

        var after = await ledger.GetBalanceAsync(walletAccount.Id);
        after.Should().Be(before);
    }

    [Fact]
    public async Task Trial_balance_matches_derived_balances_after_many_operations()
    {
        using var scope = environment.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();

        var (funding, walletAccount) = await CreateWalletAccountAsync(db);

        long expectedWallet = 0;
        var random = new Random(20260301);

        for (var i = 0; i < 25; i++)
        {
            var amount = random.Next(1_000, 500_000);
            var entry = Funding(funding, walletAccount, amount, Now.AddSeconds(i));
            await ledger.PostAsync(entry);
            expectedWallet += amount;

            if (random.NextDouble() < 0.3)
            {
                await ledger.ReverseAsync(entry.Id, "admin", Now.AddSeconds(i).AddMilliseconds(1));
                expectedWallet -= amount;
            }
        }

        (await ledger.GetBalanceAsync(walletAccount.Id)).Should().Be(expectedWallet);

        var trial = await ledger.GetTrialBalanceAsync();
        var pkr = trial.Single(line => line.Currency == Currency.PKR);
        pkr.IsBalanced.Should().BeTrue();

        // The sum of all account balances in a currency must also net to zero.
        var postings = await db.LedgerEntries
            .Where(entry => entry.Currency == Currency.PKR)
            .ToListAsync();
        postings.Sum(p => p.SignedAmountForDebitNormal()).Should().Be(0);
    }

    private static async Task<LedgerAccount> GetFundingAsync(WalletDbContext db)
    {
        var funding = await db.LedgerAccounts
            .FirstOrDefaultAsync(a => a.SystemCode == SystemAccount.Funding && a.Currency == Currency.PKR);

        if (funding is null)
        {
            funding = LedgerAccount.CreateSystem(SystemAccount.Funding, Currency.PKR, Now);
            db.LedgerAccounts.Add(funding);
            await db.SaveChangesAsync();
        }

        return funding;
    }

    private static async Task<(LedgerAccount Funding, LedgerAccount Wallet)> CreateWalletAccountAsync(
        WalletDbContext db)
    {
        var user = User.Create($"consistency+{Guid.NewGuid():N}@example.com", "hash", Role.Customer, Now);
        var wallet = Wallet.Create(
            user.Id,
            Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(),
            Currency.PKR,
            Now);
        db.Users.Add(user);
        db.Wallets.Add(wallet);

        var walletAccount = LedgerAccount.CreateForWallet(wallet.Id, Currency.PKR, Now);
        db.LedgerAccounts.Add(walletAccount);
        await db.SaveChangesAsync();

        return (await GetFundingAsync(db), walletAccount);
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
