using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 3: source and destination must differ. This is a domain invariant on the
/// <see cref="WalletPlatform.Domain.Entities.Transfer"/> aggregate as well, but the rule
/// is checked here against the resolved wallets so the failure is recorded before any
/// database write.
/// </summary>
public sealed class DistinctWalletsRule : ITransferPolicy
{
    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        if (context.DestinationWallet is null)
        {
            // A missing destination is reported by DestinationExistsRule; nothing to compare.
            return Task.FromResult(TransferPolicyResult.Success());
        }

        if (context.SourceWallet.Id == context.DestinationWallet.Id)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "SELF_TRANSFER_REJECTED",
                "Cannot transfer money to your own wallet."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}