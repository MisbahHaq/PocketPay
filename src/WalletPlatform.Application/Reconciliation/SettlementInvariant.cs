namespace WalletPlatform.Application.Reconciliation;

public static class SettlementInvariant
{
    public static bool IsHonest(long settlementBalance, long completedUnsettled, long heldReview)
        => settlementBalance == completedUnsettled + heldReview;
}
