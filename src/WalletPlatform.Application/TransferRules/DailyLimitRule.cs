using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 6: the amount + fee must not push the source wallet's completed outgoing total
/// in the rolling 24-hour window over the configured per-wallet daily limit. The rollup
/// is computed by the repository from COMPLETED transfers only (failed ones never moved
/// money, so they do not count), and the window ends at the caller's "now".
///
/// NOTE: this is a check-then-act against a derived total, so under concurrent transfers
/// two requests can both pass the check and then both post. See DECISIONS "Known gaps".
/// </summary>
public sealed class DailyLimitRule : ITransferPolicy
{
    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        var projected = context.CompletedOutgoingMinorUnits + context.TotalDebit.MinorUnits;

        if (projected > context.DailyOutgoingLimitMinorUnits)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "DAILY_LIMIT_EXCEEDED",
                $"This transfer would exceed the daily outgoing limit of " +
                $"{context.DailyOutgoingLimitMinorUnits} minor units " +
                $"(already sent {context.CompletedOutgoingMinorUnits} in the last 24 hours)."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}