using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.Policies;

/// <summary>
/// 4. Both wallets must have the same currency.
/// </summary>
public sealed class CurrencyMatchPolicy
{
    public PolicyResult Check(Currency sourceCurrency, Currency destinationCurrency)
    {
        if (sourceCurrency != destinationCurrency)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.CurrencyMismatch,
                $"Source currency ({sourceCurrency}) does not match destination currency ({destinationCurrency}).");
        }

        return PolicyResult.Success();
    }
}