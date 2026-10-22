namespace WalletPlatform.Application.Admin.GetTrialBalance;

public sealed record TrialBalanceCurrencyLine(
    string Currency,
    long TotalDebits,
    long TotalCredits,
    long Difference);

public sealed record TrialBalanceResponse(
    bool IsBalanced,
    IReadOnlyList<TrialBalanceCurrencyLine> Currencies);
