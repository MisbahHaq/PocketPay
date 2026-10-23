using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 5a: amount must be positive and within the configured min/max range, and
/// amount + fee must not exceed the configured maximum. The fee is already resolved
/// into a <see cref="Money"/> by the service, so the rule only compares magnitudes.
/// </summary>
public sealed class AmountLimitsRule : ITransferPolicy
{
    private readonly TransferOptions _options;

    public AmountLimitsRule(TransferOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        if (context.Amount.MinorUnits < _options.MinAmountMinorUnits)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "AMOUNT_BELOW_MINIMUM",
                $"Transfer amount must be at least {_options.MinAmountMinorUnits} minor units."));
        }

        if (context.Amount.MinorUnits > _options.MaxAmountMinorUnits)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "AMOUNT_ABOVE_MAXIMUM",
                $"Transfer amount must not exceed {_options.MaxAmountMinorUnits} minor units."));
        }

        var totalDebit = context.TotalDebit.MinorUnits;

        if (totalDebit < _options.MinAmountMinorUnits)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "TOTAL_DEBIT_BELOW_MINIMUM",
                "The amount plus fee is below the minimum transfer amount."));
        }

        if (totalDebit > _options.MaxAmountMinorUnits)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "TOTAL_DEBIT_ABOVE_MAXIMUM",
                "The amount plus fee exceeds the maximum transfer amount."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}