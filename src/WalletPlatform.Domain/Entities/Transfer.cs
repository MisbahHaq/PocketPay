using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Domain.Entities;

/// <summary>
/// A wallet-to-wallet transfer: an intent to move <see cref="Amount"/> from a source wallet
/// to a destination wallet, optionally charging a <see cref="Fee"/>. The transfer is the
/// business record of that intent; the money itself moves through a double-entry
/// <see cref="JournalEntry"/> referenced by <see cref="JournalEntryId"/>.
///
/// The aggregate owns its state machine:
///
///   Pending  -> Completed   (Complete: the entry posted)
///   Pending  -> Failed      (Fail: a business rule rejected the request)
///   Completed -> Reversed   (MarkReversed: an admin posted a reversal)
///
/// There are no public setters; state only ever changes through the transition methods,
/// and every illegal transition throws <see cref="TransferException"/>. This means a
/// transfer can never, for example, be completed twice or reversed while still pending.
/// </summary>
public sealed class Transfer
{
    /// <summary>Maximum length of the optional free-text note.</summary>
    public const int MaxNoteLength = 140;

    private Transfer()
    {
    }

    public Guid Id { get; private set; }
    public Guid SourceWalletId { get; private set; }
    public Guid DestinationWalletId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public Money Fee { get; private set; } = null!;
    public TransferStatus Status { get; private set; }
    public string? Note { get; private set; }

    /// <summary>
    /// The journal entry that actually moved the money. Null until the transfer completes
    /// (a failed transfer never posts anything).
    /// </summary>
    public Guid? JournalEntryId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Why the transfer failed. Only set when <see cref="Status"/> is Failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Total the source wallet is debited for: the amount plus any fee.</summary>
    public Money TotalDebit => Amount + Fee;

    /// <summary>
    /// Creates a new transfer in the <see cref="TransferStatus.Pending"/> state. Only the
    /// intrinsic invariants are checked here; policy rules that need configuration or
    /// balances (min/max amount, daily limit, sufficient funds) are enforced by the
    /// application layer. The transfer therefore starts Pending and is only marked
    /// Completed once the journal entry has posted successfully.
    /// </summary>
    public static Transfer Create(
        Guid sourceWalletId,
        Guid destinationWalletId,
        Money amount,
        Money fee,
        string? note,
        DateTimeOffset createdAt)
    {
        if (sourceWalletId == Guid.Empty)
        {
            throw new TransferException("Source wallet id is required.");
        }

        if (destinationWalletId == Guid.Empty)
        {
            throw new TransferException("Destination wallet id is required.");
        }

        if (sourceWalletId == destinationWalletId)
        {
            throw new TransferException("Source and destination wallets must differ.");
        }

        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(fee);

        if (amount.Currency != fee.Currency)
        {
            throw new TransferException("The amount and fee must be in the same currency.");
        }

        if (amount.MinorUnits <= 0)
        {
            throw new TransferException("Transfer amount must be greater than zero.");
        }

        if (amount.MinorUnits > Money.MaxMinorUnits)
        {
            throw new TransferException("Transfer amount exceeds the maximum allowed.");
        }

        if (fee.MinorUnits < 0)
        {
            throw new TransferException("Transfer fee cannot be negative.");
        }

        if (fee.MinorUnits > Money.MaxMinorUnits)
        {
            throw new TransferException("Transfer fee exceeds the maximum allowed.");
        }

        if (note is { Length: > MaxNoteLength })
        {
            throw new TransferException($"Transfer note cannot exceed {MaxNoteLength} characters.");
        }

        return new Transfer
        {
            Id = Guid.NewGuid(),
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = amount,
            Fee = fee,
            Status = TransferStatus.Pending,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// Marks the transfer completed and records the journal entry that moved the money.
    /// Legal only from <see cref="TransferStatus.Pending"/>.
    /// </summary>
    public void Complete(Guid journalEntryId, DateTimeOffset completedAt)
    {
        if (Status != TransferStatus.Pending)
        {
            throw new TransferException($"A transfer in status {Status} cannot be completed.");
        }

        if (journalEntryId == Guid.Empty)
        {
            throw new TransferException("A completed transfer requires a journal entry.");
        }

        Status = TransferStatus.Completed;
        JournalEntryId = journalEntryId;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Marks the transfer failed and records why. Legal only from
    /// <see cref="TransferStatus.Pending"/>; a failed transfer never posts a journal entry.
    /// </summary>
    public void Fail(string reason)
    {
        if (Status != TransferStatus.Pending)
        {
            throw new TransferException($"A transfer in status {Status} cannot fail.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new TransferException("A failure reason is required.");
        }

        Status = TransferStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>
    /// Marks a completed transfer reversed. Legal only from
    /// <see cref="TransferStatus.Completed"/>; the reversal journal entry is posted by the
    /// ledger service and its existence is what makes a second reversal fail.
    /// </summary>
    public void MarkReversed()
    {
        if (Status != TransferStatus.Completed)
        {
            throw new TransferException($"Only a completed transfer can be reversed; this one is {Status}.");
        }

        Status = TransferStatus.Reversed;
    }
}
