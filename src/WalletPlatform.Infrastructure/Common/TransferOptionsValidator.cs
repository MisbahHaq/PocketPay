using Microsoft.Extensions.Options;
using WalletPlatform.Application.Common;

namespace WalletPlatform.Infrastructure.Common;

internal sealed class TransferOptionsValidator : IValidateOptions<TransferOptions>
{
    public ValidateOptionsResult Validate(string? name, TransferOptions options)
    {
        var failures = new List<string>();

        if (options.FlatFeeMinorUnits < 0)
        {
            failures.Add("Transfer:FlatFeeMinorUnits cannot be negative.");
        }

        if (options.MinAmountMinorUnits <= 0)
        {
            failures.Add("Transfer:MinAmountMinorUnits must be greater than zero.");
        }

        if (options.MaxAmountMinorUnits < options.MinAmountMinorUnits)
        {
            failures.Add("Transfer:MaxAmountMinorUnits must be greater than or equal to MinAmountMinorUnits.");
        }

        if (options.DailyOutgoingLimitMinorUnits <= 0)
        {
            failures.Add("Transfer:DailyOutgoingLimitMinorUnits must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}