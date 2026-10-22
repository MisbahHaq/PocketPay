using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Application.Abstractions.Persistence;

/// <summary>
/// The write/read boundary for the double-entry ledger. Posting is always transactional
/// and atomic: either the whole journal entry (and all its postings) is persisted, or
/// nothing is. Balances are derived from postings on demand and never stored.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Persists a complete, balanced journal entry together with all its postings inside a
    /// single transaction. The entry must already satisfy the domain invariants
    /// (built via <see cref="JournalEntry.Build"/>).
    /// </summary>
    Task PostAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses an existing journal entry by posting its mirror image (debits and credits
    /// swapped) as a new entry that references the original. The original is never edited.
    /// Throws <see cref="WalletPlatform.Application.Common.Exceptions.NotFoundException"/>
    /// when the entry does not exist and
    /// <see cref="WalletPlatform.Application.Common.Exceptions.ConflictException"/> when it
    /// has already been reversed (an entry can be reversed at most once).
    /// </summary>
    Task<JournalEntry> ReverseAsync(
        Guid journalEntryId,
        string createdBy,
        DateTimeOffset reversedAtUtc,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current signed balance of a ledger account, or null when the account
    /// does not exist. Signed positive means a normal balance for the account type.
    /// </summary>
    Task<long?> GetBalanceAsync(Guid ledgerAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of statement lines for an account, oldest-first, each carrying the
    /// running balance after that posting. Throws when the account does not exist.
    /// </summary>
    Task<LedgerStatement> GetStatementAsync(
        Guid ledgerAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single statement line as produced by the ledger service, before it is mapped to a
/// transport DTO. The running balance is computed in the database with a window function.
/// </summary>
public sealed record LedgerStatementLine(
    Guid JournalEntryId,
    DateTimeOffset CreatedAtUtc,
    Direction Direction,
    long AmountMinorUnits,
    long RunningBalanceMinorUnits,
    string? Type,
    string? Reference,
    string? Description);

/// <summary>Statement page plus the account's closing balance.</summary>
public sealed record LedgerStatement(
    Guid LedgerAccountId,
    Guid? WalletId,
    Currency Currency,
    long ClosingBalanceMinorUnits,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<LedgerStatementLine> Lines);
