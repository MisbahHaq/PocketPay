using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 2: the destination wallet must exist. Identified by wallet number, so the caller
/// never knows whether they are allowed to see it — we simply return "not found" when the
/// number is unknown (see DECISIONS: 404-over-403).
/// </summary>
public sealed class DestinationExistsRule : ITransferPolicy
{
    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        if (context.DestinationWallet is null)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "DESTINATION_WALLET_NOT_FOUND",
                "A wallet with that number was not found."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}