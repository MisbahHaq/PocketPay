using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.Policies;

/// <summary>
/// 6. The sum of completed transfers from the source wallet in the last 24 hours must not
/// exceed the daily outgoing limit.
/// </summary>
public sealed class DailyLimitPolicy
{
    private readonly TransferOptions _options;
    private readonly IClock _clock;

    public DailyLimitPolicy(TransferOptions options, IClock clock)
    {
        _options = options;
        _clock = clock;
    }

    public PolicyResult Check(long completedOutgoingSumMinorUnits, Money amount, Money fee)
    {
        var windowStart = _clock.UtcNow.AddHours(-24);
        var totalDebit = amount + fee;
        var projected = completedOutgoingSumMinorUnits + totalDebit.MinorUnits;

        if (projected > _options.DailyOutgoingLimitMinorUnits)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.DailyLimitExceeded,
                $"Daily outgoing limit of {_options.DailyOutgoingLimitMinorUnits} minor units " +
                $"would be exceeded (current: {completedOutgoingSumMinorUnits}, this transfer: {totalDebit.MinorUnits}).");
        }

        return PolicyResult.Success();
    }
}