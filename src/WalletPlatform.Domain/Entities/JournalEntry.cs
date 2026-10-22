using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Domain.Entities;

/// <summary>
/// A journal entry is ONE business event (a funding, later a transfer or a bill payment)
/// recorded as a set of postings that must balance. It is the aggregate root: postings
/// only ever come into existence through <see cref="Debit"/> / <see cref="Credit"/> on a
/// journal entry, so it can never be persisted in an unbalanced state.
///
/// Invariants enforced here (in the domain):
///  1. At least two postings.
///  2. Per currency, total debits == total credits.
///  3. Every posting amount is strictly positive.
///  4. A posting's currency matches its account's currency.
/// (Invariant 5 — immutability — is enforced at the database level and by the fact that
/// this type exposes no mutators.)
/// </summary>
public sealed class JournalEntry
{
    private readonly List<LedgerEntry> _postings = [];

    private JournalEntry()
    {
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string? Reference { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = null!;

    /// <summary>Set when this entry is a reversal of an earlier entry.</summary>
    public Guid? ReversesJournalEntryId { get; private set; }

    public IReadOnlyList<LedgerEntry> Postings => _postings.AsReadOnly();

    public bool IsReversal => ReversesJournalEntryId is not null;

    /// <summary>Convenience constructor that starts an empty builder.</summary>
    public static JournalEntry Create(
        string type,
        string createdBy,
        DateTimeOffset createdAtUtc,
        string? reference = null,
        string? description = null,
        Guid? reversesJournalEntryId = null)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new LedgerException("Journal entry type is required.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new LedgerException("Journal entry creator is required.");
        }

        return new JournalEntry
        {
            Id = Guid.NewGuid(),
            Type = type,
            Reference = reference,
            Description = description,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = createdBy,
            ReversesJournalEntryId = reversesJournalEntryId,
        };
    }

    /// <summary>Adds a debit posting. Returns the entry so calls can be chained.</summary>
    public JournalEntry Debit(LedgerAccount account, Money amount)
    {
        AddPosting(account, Direction.Debit, amount);
        return this;
    }

    /// <summary>Adds a credit posting. Returns the entry so calls can be chained.</summary>
    public JournalEntry Credit(LedgerAccount account, Money amount)
    {
        AddPosting(account, Direction.Credit, amount);
        return this;
    }

    /// <summary>
    /// Validates invariants 1 and 2 and returns the entry, ready to be persisted.
    /// Callers must invoke this before persisting; <see cref="LedgerService"/> does so.
    /// </summary>
    public JournalEntry Build()
    {
        if (_postings.Count < 2)
        {
            throw new LedgerException("A journal entry must have at least two postings.");
        }

        var totalsByCurrency = _postings
            .GroupBy(posting => posting.Currency)
            .Select(group => new
            {
                Currency = group.Key,
                Debits = group.Where(p => p.Direction == Direction.Debit).Sum(p => p.AmountMinorUnits),
                Credits = group.Where(p => p.Direction == Direction.Credit).Sum(p => p.AmountMinorUnits),
            });

        foreach (var totals in totalsByCurrency)
        {
            if (totals.Debits != totals.Credits)
            {
                throw new LedgerException(
                    $"Journal entry is unbalanced for {totals.Currency}: " +
                    $"debits {totals.Debits} != credits {totals.Credits}.");
            }
        }

        return this;
    }

    private void AddPosting(LedgerAccount account, Direction direction, Money amount)
    {
        if (account is null)
        {
            throw new LedgerException("A posting requires an account.");
        }

        // Invariants 3 (positive) and 4 (currency match) are enforced by LedgerEntry.Create.
        _postings.Add(LedgerEntry.Create(Id, account, direction, amount, CreatedAtUtc));
    }
}
