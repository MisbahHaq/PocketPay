using FluentAssertions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.UnitTests.Transfers;

public sealed class TransferTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Transfer CreateTransfer(
        long amountMinorUnits = 100_000,
        long feeMinorUnits = 0,
        Currency currency = Currency.PKR,
        string? note = null) =>
        Transfer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Money(amountMinorUnits, currency),
            new Money(feeMinorUnits, currency),
            note,
            Now);

    [Fact]
    public void Newly_created_transfer_is_pending_and_unposted()
    {
        var transfer = CreateTransfer();

        transfer.Status.Should().Be(TransferStatus.Pending);
        transfer.JournalEntryId.Should().BeNull();
        transfer.CompletedAt.Should().BeNull();
        transfer.FailureReason.Should().BeNull();
        transfer.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Total_debit_is_amount_plus_fee()
    {
        var transfer = CreateTransfer(amountMinorUnits: 100_000, feeMinorUnits: 2_500);

        transfer.TotalDebit.Should().Be(new Money(102_500, Currency.PKR));
    }

    [Fact]
    public void Total_debit_equals_amount_when_fee_is_zero()
    {
        var transfer = CreateTransfer(amountMinorUnits: 100_000, feeMinorUnits: 0);

        transfer.TotalDebit.Should().Be(new Money(100_000, Currency.PKR));
    }

    [Fact]
    public void Blank_note_is_normalised_to_null()
    {
        var transfer = CreateTransfer(note: "   ");

        transfer.Note.Should().BeNull();
    }

    [Fact]
    public void Note_is_trimmed()
    {
        var transfer = CreateTransfer(note: "  rent for June  ");

        transfer.Note.Should().Be("rent for June");
    }

    // ---- Legal transitions -------------------------------------------------------------

    [Fact]
    public void Pending_transfer_can_complete_with_a_journal_entry()
    {
        var transfer = CreateTransfer();
        var journalEntryId = Guid.NewGuid();

        transfer.Complete(journalEntryId, Now.AddMinutes(1));

        transfer.Status.Should().Be(TransferStatus.Completed);
        transfer.JournalEntryId.Should().Be(journalEntryId);
        transfer.CompletedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Pending_transfer_can_fail_with_a_reason()
    {
        var transfer = CreateTransfer();

        transfer.Fail("INSUFFICIENT_FUNDS");

        transfer.Status.Should().Be(TransferStatus.Failed);
        transfer.FailureReason.Should().Be("INSUFFICIENT_FUNDS");
        transfer.JournalEntryId.Should().BeNull();
    }

    [Fact]
    public void Completed_transfer_can_be_reversed()
    {
        var transfer = CreateTransfer();
        transfer.Complete(Guid.NewGuid(), Now.AddMinutes(1));

        transfer.MarkReversed();

        transfer.Status.Should().Be(TransferStatus.Reversed);
    }

    // ---- Illegal transitions -----------------------------------------------------------

    [Fact]
    public void Completed_transfer_cannot_complete_again()
    {
        var transfer = CreateTransfer();
        transfer.Complete(Guid.NewGuid(), Now);

        var act = () => transfer.Complete(Guid.NewGuid(), Now);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Completed_transfer_cannot_fail()
    {
        var transfer = CreateTransfer();
        transfer.Complete(Guid.NewGuid(), Now);

        var act = () => transfer.Fail("TOO_LATE");

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Failed_transfer_cannot_complete()
    {
        var transfer = CreateTransfer();
        transfer.Fail("INSUFFICIENT_FUNDS");

        var act = () => transfer.Complete(Guid.NewGuid(), Now);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Failed_transfer_cannot_fail_again()
    {
        var transfer = CreateTransfer();
        transfer.Fail("INSUFFICIENT_FUNDS");

        var act = () => transfer.Fail("DAILY_LIMIT_EXCEEDED");

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Pending_transfer_cannot_be_reversed()
    {
        var transfer = CreateTransfer();

        var act = () => transfer.MarkReversed();

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Failed_transfer_cannot_be_reversed()
    {
        var transfer = CreateTransfer();
        transfer.Fail("INSUFFICIENT_FUNDS");

        var act = () => transfer.MarkReversed();

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Reversed_transfer_cannot_be_reversed_again()
    {
        var transfer = CreateTransfer();
        transfer.Complete(Guid.NewGuid(), Now);
        transfer.MarkReversed();

        var act = () => transfer.MarkReversed();

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Reversed_transfer_cannot_complete()
    {
        var transfer = CreateTransfer();
        transfer.Complete(Guid.NewGuid(), Now);
        transfer.MarkReversed();

        var act = () => transfer.Complete(Guid.NewGuid(), Now);

        act.Should().Throw<TransferException>();
    }

    // ---- Creation invariants -----------------------------------------------------------

    [Fact]
    public void Creating_a_transfer_to_the_same_wallet_is_rejected()
    {
        var walletId = Guid.NewGuid();

        var act = () => Transfer.Create(
            walletId, walletId, new Money(1, Currency.PKR), Money.Zero(Currency.PKR), null, Now);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Amount_and_fee_must_share_a_currency()
    {
        var act = () => Transfer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Money(100, Currency.PKR),
            new Money(1, Currency.USD),
            null,
            Now);

        act.Should().Throw<TransferException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amount_must_be_positive(long amount)
    {
        var act = () => CreateTransfer(amountMinorUnits: amount);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Fee_cannot_be_negative()
    {
        var act = () => CreateTransfer(feeMinorUnits: -1);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Amount_cannot_exceed_the_hard_cap()
    {
        var act = () => CreateTransfer(amountMinorUnits: Money.MaxMinorUnits + 1);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Note_cannot_exceed_max_length()
    {
        var tooLong = new string('x', Transfer.MaxNoteLength + 1);

        var act = () => CreateTransfer(note: tooLong);

        act.Should().Throw<TransferException>();
    }

    [Fact]
    public void Completing_requires_a_journal_entry()
    {
        var transfer = CreateTransfer();

        var act = () => transfer.Complete(Guid.Empty, Now);

        act.Should().Throw<TransferException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Failing_requires_a_reason(string reason)
    {
        var transfer = CreateTransfer();

        var act = () => transfer.Fail(reason);

        act.Should().Throw<TransferException>();
    }
}
