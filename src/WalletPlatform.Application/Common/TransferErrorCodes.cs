namespace WalletPlatform.Application.Common;

/// <summary>
/// Stable, machine-readable error codes returned in ProblemDetails.extensions["code"] when
/// a transfer fails a business rule. These are intentionally NOT HTTP status codes — they
/// let the client distinguish INSUFFICIENT_FUNDS from DAILY_LIMIT_EXCEEDED without
/// parsing human-readable text.
/// </summary>
public static class TransferErrorCodes
{
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string DailyLimitExceeded = "DAILY_LIMIT_EXCEEDED";
    public const string AmountBelowMinimum = "AMOUNT_BELOW_MINIMUM";
    public const string AmountAboveMaximum = "AMOUNT_ABOVE_MAXIMUM";
    public const string SameWallet = "SAME_WALLET";
    public const string CurrencyMismatch = "CURRENCY_MISMATCH";
    public const string DestinationNotFound = "DESTINATION_NOT_FOUND";
    public const string SourceWalletNotFound = "SOURCE_WALLET_NOT_FOUND";
}