namespace WalletPlatform.Application.Reconciliation;

/// <summary>
/// Pure matching decision for our payment vs. the biller's settlement rows.
/// Kept free of EF/HTTP concerns so it can be property-tested and reused by
/// import/processing paths.
/// </summary>
public static class ReconciliationMatcher
{
    public const string Matched = "Matched";
    public const string MissingAtBiller = "MissingAtBiller";
    public const string UnknownAtOurSide = "UnknownAtOurSide";
    public const string AmountMismatch = "AmountMismatch";

    public static string Categorize(
        bool hasOurPayment,
        bool hasBillerRows,
        long ourAmount,
        long billerAmount,
        string? billerStatus,
        int billerRowCount)
    {
        if (!hasOurPayment)
        {
            return UnknownAtOurSide;
        }

        if (!hasBillerRows)
        {
            return MissingAtBiller;
        }

        var singleRow = billerRowCount == 1;
        var success = string.Equals(billerStatus, "Success", StringComparison.OrdinalIgnoreCase);

        return singleRow && success && ourAmount == billerAmount ? Matched : AmountMismatch;
    }
}
