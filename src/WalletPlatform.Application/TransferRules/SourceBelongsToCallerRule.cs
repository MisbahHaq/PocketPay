using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 1: the source wallet must belong to the caller. Admins cannot transfer on
/// behalf of users through this endpoint — that is a separate admin operation. We also
/// reject an unknown wallet here with a 404 (see DECISIONS: 404-over-403 for resources
/// the caller is not authorised to see).
/// </summary>
public sealed class SourceBelongsToCallerRule : ITransferPolicy
{
    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        if (context.SourceWallet.UserId != context.CallerId)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "SOURCE_WALLET_NOT_OWNED",
                "The source wallet does not belong to the authenticated user."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}