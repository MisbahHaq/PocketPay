namespace WalletPlatform.Domain.Exceptions;

/// <summary>
/// Thrown when a ledger invariant is violated while building a journal entry or an
/// account (e.g. an unbalanced entry, a non-positive amount, or a currency mismatch).
///
/// This is a domain exception: it signals that the requested operation makes no
/// accounting sense and must never be turned into a database row. The transport layer
/// maps it to a 400/409 rather than a 500.
/// </summary>
public sealed class LedgerException(string message) : Exception(message);
