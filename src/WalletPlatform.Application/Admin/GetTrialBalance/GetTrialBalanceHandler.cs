using WalletPlatform.Application.Abstractions.Persistence;

namespace WalletPlatform.Application.Admin.GetTrialBalance;

/// <summary>
/// Reports, per currency, total debits versus total credits across the entire ledger.
/// For a healthy double-entry ledger every currency balances (difference == 0). This is
/// the fastest way to prove the books are internally consistent.
/// </summary>
public sealed class GetTrialBalanceHandler(ILedgerService ledger)
{
    public async Task<TrialBalanceResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var lines = await ledger.GetTrialBalanceAsync(cancellationToken);

        var currencyLines = lines
            .Select(line => new TrialBalanceCurrencyLine(
                line.Currency.ToString(),
                line.TotalDebits,
                line.TotalCredits,
                line.Difference))
            .ToList();

        return new TrialBalanceResponse(
            IsBalanced: currencyLines.All(line => line.Difference == 0),
            currencyLines);
    }
}
