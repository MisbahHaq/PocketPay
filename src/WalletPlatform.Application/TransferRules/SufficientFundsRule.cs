using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.TransferRules;

/// <summary>
/// Rule 5b: amount + fee must not exceed the source wallet's available balance. The
/// balance is derived from the ledger (see ADR-008) and passed in by the service; this
/// rule only compares magnitudes, so it stays a pure unit-testable function.
///
/// NOTE: this is a check-then-act. It reads the balance and, later, posts against it,
/// with no row lock in between — see DECISIONS "Known gaps" for the concurrency risk.
/// </summary>
public sealed class SufficientFundsRule : ITransferPolicy
{
    public Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken)
    {
        var totalDebit = context.TotalDebit.MinorUnits;

        if (context.SourceBalanceMinorUnits < totalDebit)
        {
            return Task.FromResult(TransferPolicyResult.Fail(
                "INSUFFICIENT_FUNDS",
                $"Source wallet balance {context.SourceBalanceMinorUnits} minor units is less than " +
                $"the required {totalDebit} minor units (amount + fee)."));
        }

        return Task.FromResult(TransferPolicyResult.Success());
    }
}