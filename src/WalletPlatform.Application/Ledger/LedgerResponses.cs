namespace WalletPlatform.Application.Ledger;

/// <summary>
/// The current balance of a ledger account, derived from its postings (never stored).
/// <see cref="SignedMinorUnits"/> is positive when the account holds a normal balance
/// (a credit balance for a liability/revenue account, a debit balance for an asset).
/// </summary>
public sealed record BalanceResponse(
    Guid LedgerAccountId,
    Guid? WalletId,
    string Currency,
    string AccountType,
    long SignedMinorUnits);

/// <summary>One line of an account statement, including the running balance after it.</summary>
public sealed record StatementLine(
    Guid JournalEntryId,
    DateTimeOffset CreatedAtUtc,
    string Direction,
    long AmountMinorUnits,
    long RunningBalanceMinorUnits,
    string? Type,
    string? Reference,
    string? Description);

/// <summary>
/// A page of statement lines for one account plus the closing balance. The lines are
/// ordered oldest-first so the running balance reads top to bottom.
/// </summary>
public sealed record StatementResponse(
    Guid LedgerAccountId,
    Guid? WalletId,
    string Currency,
    long ClosingBalanceMinorUnits,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<StatementLine> Lines);
