using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 4: same currency on both wallets. PKR only for now, but we check it rather than
/// assume it, so a future multi-currency rollout fails loudly instead of posting a
/// cross-currency transfer that the ledger would then reject.
/// </summary>
public sealed class SameCurrencyRule : ITransferPolicy
{
    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        if (context.DestinationWallet is null)
        {
            return Task.FromResult(TransferPolicyResult.Success());
        }

        if (context.SourceWallet.Currency != context.DestinationWallet.Currency)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "CURRENCY_MISMATCH",
                $"Source wallet is {context.SourceWallet.Currency} but destination wallet is {context.DestinationWallet.Currency}."));
        }

        if (context.Amount.Currency != context.SourceWallet.Currency)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "CURRENCY_MISMATCH",
                "The transfer amount currency does not match the source wallet currency."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}