namespace WalletPlatform.Domain.Exceptions;

/// <summary>
/// Thrown when a transfer violates a domain invariant or an illegal state transition is
/// attempted (e.g. completing a failed transfer, or transferring to the source wallet).
///
/// This is a domain exception: it signals the requested operation must never be persisted
/// as-is. The transport layer maps it (or a more specific rule failure it carries) to a
/// 4xx rather than a 500.
///
/// Deliberately NOT sealed: <see cref="TransferPolicyException"/> derives from it to attach
/// a stable machine-readable <c>Code</c> that the ProblemDetails response copies into the
/// <c>code</c> extension.
/// </summary>
public class TransferException(string message) : Exception(message)
{
}