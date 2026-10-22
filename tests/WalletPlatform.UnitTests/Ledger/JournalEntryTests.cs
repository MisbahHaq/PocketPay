using FluentAssertions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.UnitTests.Ledger;

public sealed class JournalEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static LedgerAccount Wallet(Currency currency = Currency.PKR) =>
        LedgerAccount.CreateForWallet(Guid.NewGuid(), currency, Now);

    private static LedgerAccount Funding(Currency currency = Currency.PKR) =>
        LedgerAccount.CreateSystem(SystemAccount.Funding, currency, Now);

    private static JournalEntry NewEntry() =>
        JournalEntry.Create("Funding", "test", Now);

    [Fact]
    public void Balanced_entry_builds_successfully()
    {
        var wallet = Wallet();
        var funding = Funding();

        var entry = NewEntry()
            .Debit(funding, new Money(100_000, Currency.PKR))
            .Credit(wallet, new Money(100_000, Currency.PKR))
            .Build();

        entry.Postings.Should().HaveCount(2);
        entry.Postings.Count(p => p.Direction == Direction.Debit).Should().Be(1);
        entry.Postings.Count(p => p.Direction == Direction.Credit).Should().Be(1);
    }

    [Fact]
    public void Debits_equal_credits_per_currency_is_required()
    {
        var wallet = Wallet();
        var funding = Funding();

        var act = () => NewEntry()
            .Debit(funding, new Money(100_000, Currency.PKR))
            .Credit(wallet, new Money(99_999, Currency.PKR))
            .Build();

        act.Should().Throw<LedgerException>().WithMessage("*unbalanced*PKR*");
    }

    [Fact]
    public void Single_posting_entry_is_rejected()
    {
        var funding = Funding();

        var act = () => NewEntry()
            .Debit(funding, new Money(100, Currency.PKR))
            .Build();

        act.Should().Throw<LedgerException>().WithMessage("*at least two postings*");
    }

    [Fact]
    public void Empty_entry_is_rejected()
    {
        var act = () => NewEntry().Build();

        act.Should().Throw<LedgerException>().WithMessage("*at least two postings*");
    }

    [Fact]
    public void Zero_amount_posting_is_rejected()
    {
        var wallet = Wallet();
        var funding = Funding();

        var act = () => NewEntry()
            .Debit(funding, Money.Zero(Currency.PKR))
            .Credit(wallet, Money.Zero(Currency.PKR));

        act.Should().Throw<LedgerException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void Negative_amount_posting_is_rejected()
    {
        var wallet = Wallet();
        var funding = Funding();

        var act = () => NewEntry()
            .Debit(funding, new Money(-100, Currency.PKR))
            .Credit(wallet, new Money(-100, Currency.PKR));

        act.Should().Throw<LedgerException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void Posting_currency_must_match_account_currency()
    {
        var pkrWallet = Wallet(Currency.PKR);
        var funding = Funding();

        var act = () => NewEntry()
            .Debit(funding, new Money(100, Currency.PKR))
            .Credit(pkrWallet, new Money(100, Currency.USD));

        act.Should().Throw<LedgerException>().WithMessage("*currency*");
    }

    [Fact]
    public void Multiple_currencies_may_each_balance_within_one_entry()
    {
        var pkrWallet = Wallet(Currency.PKR);
        var pkrFunding = Funding(Currency.PKR);
        var usdWallet = Wallet(Currency.USD);
        var usdFunding = Funding(Currency.USD);

        var entry = NewEntry()
            .Debit(pkrFunding, new Money(100_000, Currency.PKR))
            .Credit(pkrWallet, new Money(100_000, Currency.PKR))
            .Debit(usdFunding, new Money(500_00, Currency.USD))
            .Credit(usdWallet, new Money(500_00, Currency.USD))
            .Build();

        entry.Postings.Should().HaveCount(4);
    }

    [Fact]
    public void Mixed_currencies_that_do_not_each_balance_are_rejected()
    {
        var pkrWallet = Wallet(Currency.PKR);
        var pkrFunding = Funding(Currency.PKR);
        var usdWallet = Wallet(Currency.USD);
        var usdFunding = Funding(Currency.USD);

        var act = () => NewEntry()
            .Debit(pkrFunding, new Money(100_000, Currency.PKR))
            .Credit(pkrWallet, new Money(100_000, Currency.PKR))
            .Debit(usdFunding, new Money(500_00, Currency.USD))
            .Credit(usdWallet, new Money(400_00, Currency.USD))
            .Build();

        act.Should().Throw<LedgerException>().WithMessage("*USD*");
    }

    [Fact]
    public void Type_and_creator_are_required()
    {
        var actType = () => JournalEntry.Create("", "test", Now).Debit(Funding(), new Money(1, Currency.PKR));
        var actCreator = () => JournalEntry.Create("Funding", "  ", Now);

        actType.Should().Throw<LedgerException>();
        actCreator.Should().Throw<LedgerException>();
    }

    [Fact]
    public void Posting_direction_carries_the_sign_for_balances()
    {
        var wallet = Wallet();
        var funding = Funding();

        var entry = NewEntry()
            .Debit(funding, new Money(100_000, Currency.PKR))
            .Credit(wallet, new Money(100_000, Currency.PKR))
            .Build();

        var walletPosting = entry.Postings.Single(p => p.LedgerAccountId == wallet.Id);
        var fundingPosting = entry.Postings.Single(p => p.LedgerAccountId == funding.Id);

        walletPosting.SignedAmountForCreditNormal().Should().Be(100_000);
        fundingPosting.SignedAmountForDebitNormal().Should().Be(100_000);
    }
}
