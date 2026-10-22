using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.UnitTests.Ledger;

/// <summary>
/// Property-based tests for the ledger's core invariants. Unlike example-based tests,
/// these state a rule that must hold for ALL inputs and let FsCheck generate hundreds of
/// cases, shrinking any failure to a minimal counter-example. This is how we gain
/// confidence that the balance rule cannot be circumvented by an input we never thought of.
/// </summary>
public sealed class LedgerPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Gen<long> PositiveAmounts =
        Gen.Choose(1, int.MaxValue).Select(value => (long)value);

    private static LedgerAccount WalletAccount() =>
        LedgerAccount.CreateForWallet(Guid.NewGuid(), Currency.PKR, Now);

    private static LedgerAccount FundingAccount() =>
        LedgerAccount.CreateSystem(SystemAccount.Funding, Currency.PKR, Now);

    [Property(MaxTest = 500)]
    public Property Any_positive_debit_credit_pair_builds_and_balances()
    {
        return Prop.ForAll(
            Arb.From(PositiveAmounts),
            amount =>
            {
                var entry = JournalEntry.Create("Funding", "test", Now)
                    .Debit(FundingAccount(), new Money(amount, Currency.PKR))
                    .Credit(WalletAccount(), new Money(amount, Currency.PKR))
                    .Build();

                return entry.Postings.Count == 2;
            });
    }

    [Property(MaxTest = 500)]
    public Property A_built_entry_is_always_zero_sum()
    {
        return Prop.ForAll(
            Arb.From(PositiveAmounts),
            amount =>
            {
                var entry = JournalEntry.Create("Funding", "test", Now)
                    .Debit(FundingAccount(), new Money(amount, Currency.PKR))
                    .Credit(WalletAccount(), new Money(amount, Currency.PKR))
                    .Build();

                // Summing the signed value of every posting must be exactly zero, which is
                // the definition of a balanced double-entry entry.
                var total = entry.Postings.Sum(p => p.SignedAmountForDebitNormal());
                return total == 0;
            });
    }

    [Property(MaxTest = 500)]
    public Property Unbalanced_entries_are_always_rejected()
    {
        return Prop.ForAll(
            Arb.From(PositiveAmounts),
            Arb.From(PositiveAmounts),
            (debit, credit) =>
            {
                if (debit == credit)
                {
                    return true; // only unbalanced pairs are under test
                }

                try
                {
                    JournalEntry.Create("Funding", "test", Now)
                        .Debit(FundingAccount(), new Money(debit, Currency.PKR))
                        .Credit(WalletAccount(), new Money(credit, Currency.PKR))
                        .Build();

                    return false; // building should have thrown
                }
                catch (LedgerException)
                {
                    return true;
                }
            });
    }

    [Property(MaxTest = 500)]
    public Property Reversal_always_nets_to_zero()
    {
        return Prop.ForAll(
            Arb.From(PositiveAmounts),
            amount =>
            {
                var wallet = WalletAccount();
                var funding = FundingAccount();

                var original = JournalEntry.Create("Funding", "test", Now)
                    .Debit(funding, new Money(amount, Currency.PKR))
                    .Credit(wallet, new Money(amount, Currency.PKR))
                    .Build();

                var accounts = new Dictionary<Guid, LedgerAccount>
                {
                    [wallet.Id] = wallet,
                    [funding.Id] = funding,
                };

                var reversal = JournalEntry.CreateReversal(original, accounts, "admin", Now.AddMinutes(1));

                // Original + reversal, per account, must sum to zero.
                var perAccount = original.Postings
                    .Concat(reversal.Postings)
                    .GroupBy(p => p.LedgerAccountId)
                    .Select(g => g.Sum(p => p.SignedAmountForDebitNormal()));

                return perAccount.All(signed => signed == 0);
            });
    }

    [Property(MaxTest = 500)]
    public Property Multi_currency_entries_balance_per_currency()
    {
        return Prop.ForAll(
            Arb.From(PositiveAmounts),
            Arb.From(PositiveAmounts),
            (pkrAmount, usdAmount) =>
            {
                var pkrWallet = WalletAccount();
                var pkrFunding = FundingAccount();
                var usdWallet = LedgerAccount.CreateForWallet(Guid.NewGuid(), Currency.USD, Now);
                var usdFunding = LedgerAccount.CreateSystem(SystemAccount.Funding, Currency.USD, Now);

                var entry = JournalEntry.Create("Funding", "test", Now)
                    .Debit(pkrFunding, new Money(pkrAmount, Currency.PKR))
                    .Credit(pkrWallet, new Money(pkrAmount, Currency.PKR))
                    .Debit(usdFunding, new Money(usdAmount, Currency.USD))
                    .Credit(usdWallet, new Money(usdAmount, Currency.USD))
                    .Build();

                return entry.Postings
                    .GroupBy(p => p.Currency)
                    .Select(g => new
                    {
                        Debits = g.Where(p => p.Direction == Direction.Debit).Sum(p => p.AmountMinorUnits),
                        Credits = g.Where(p => p.Direction == Direction.Credit).Sum(p => p.AmountMinorUnits),
                    })
                    .All(totals => totals.Debits == totals.Credits);
            });
    }

    [Property(MaxTest = 500)]
    public Property Non_positive_amounts_are_always_rejected()
    {
        var gen = Gen.Choose(int.MinValue, 0).Select(value => (long)value);
        return Prop.ForAll(
            Arb.From(gen),
            value =>
            {
                try
                {
                    JournalEntry.Create("Funding", "test", Now)
                        .Debit(FundingAccount(), new Money(value, Currency.PKR))
                        .Credit(WalletAccount(), new Money(value, Currency.PKR));

                    return false; // adding a non-positive posting should have thrown
                }
                catch (LedgerException)
                {
                    return true;
                }
            });
    }
}
