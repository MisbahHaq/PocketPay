using WalletPlatform.Domain.Exceptions;

namespace WalletPlatform.Domain.Exceptions;

/// <summary>
/// Thrown when a transfer request is rejected by a business rule (insufficient funds,
/// daily limit, self-transfer, etc.). Carries a stable machine-readable <see cref="Code"/>
/// that the transport layer copies into the <c>code</c> extension of the ProblemDetails
/// response, so clients can branch on the failure reason without parsing prose.
/// </summary>
public sealed class TransferPolicyException(string code, string message) : TransferException(message)
{
    /// <summary>Stable machine-readable code, e.g. INSUFFICIENT_FUNDS.</summary>
    public string Code { get; } = code;
}