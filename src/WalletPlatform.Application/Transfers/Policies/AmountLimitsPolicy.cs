using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.Policies;

/// <summary>
/// 5. Amount must be > 0, within configured min/max, and amount + fee must not exceed
/// the source wallet's available balance (derived from ledger).
/// </summary>
public sealed class AmountLimitsPolicy
{
    private readonly TransferOptions _options;

    public AmountLimitsPolicy(TransferOptions options)
    {
        _options = options;
    }

    public PolicyResult Check(Money amount, Money fee, long availableBalanceMinorUnits)
    {
        if (amount.MinorUnits < _options.MinAmountMinorUnits)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.AmountBelowMinimum,
                $"Transfer amount must be at least {_options.MinAmountMinorUnits} minor units.");
        }

        if (amount.MinorUnits > _options.MaxAmountMinorUnits)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.AmountAboveMaximum,
                $"Transfer amount must not exceed {_options.MaxAmountMinorUnits} minor units.");
        }

        var totalDebit = amount + fee;
        if (totalDebit.MinorUnits > availableBalanceMinorUnits)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.InsufficientFunds,
                $"Insufficient funds: available balance is {availableBalanceMinorUnits} minor units, " +
                $"but transfer requires {totalDebit.MinorUnits} (amount {amount.MinorUnits} + fee {fee.MinorUnits}).");
        }

        return PolicyResult.Success();
    }
}