using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Domain.Entities;

/// <summary>
/// A single posting in the ledger: "debit (or credit) this account by this amount".
///
/// A posting is IMMUTABLE. There are no public setters and no updatable methods by
/// design — once written, a ledger row is never updated or deleted. A mistake is fixed
/// later with a compensating (reversal) journal entry.
///
/// The amount is always positive; the <see cref="Direction"/> carries the sign. That
/// keeps "amount" a magnitude and makes "sum of debits == sum of credits" a simple,
/// unambiguous check.
/// </summary>
public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public Guid LedgerAccountId { get; private set; }
    public Direction Direction { get; private set; }
    public long AmountMinorUnits { get; private set; }
    public Currency Currency { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static LedgerEntry Create(
        Guid journalEntryId,
        LedgerAccount account,
        Direction direction,
        Money amount,
        DateTimeOffset createdAtUtc)
    {
        if (journalEntryId == Guid.Empty)
        {
            throw new LedgerException("A posting must belong to a journal entry.");
        }

        if (amount.MinorUnits <= 0)
        {
            throw new LedgerException("Posting amounts must be greater than zero.");
        }

        if (amount.Currency != account.Currency)
        {
            throw new LedgerException(
                $"Posting currency {amount.Currency} does not match account currency {account.Currency}.");
        }

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            JournalEntryId = journalEntryId,
            LedgerAccountId = account.Id,
            Direction = direction,
            AmountMinorUnits = amount.MinorUnits,
            Currency = amount.Currency,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>The signed effect of this posting on a debit-normal account balance.</summary>
    public long SignedAmountForDebitNormal() =>
        Direction == Direction.Debit ? AmountMinorUnits : -AmountMinorUnits;

    /// <summary>The signed effect of this posting on a credit-normal account balance.</summary>
    public long SignedAmountForCreditNormal() =>
        Direction == Direction.Credit ? AmountMinorUnits : -AmountMinorUnits;
}
