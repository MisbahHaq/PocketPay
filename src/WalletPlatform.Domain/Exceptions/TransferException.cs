namespace WalletPlatform.Domain.Exceptions;

/// <summary>
/// Thrown when a transfer violates a domain invariant or an illegal state transition is
/// attempted (e.g. completing a failed transfer, or transferring to the source wallet).
///
/// This is a domain exception: it signals the requested operation must never be persisted
/// as-is. The transport layer maps it (or a more specific rule failure it carries) to a
/// 4xx rather than a 500.
/// </summary>
public sealed class TransferException(string message) : Exception(message)
{
}
