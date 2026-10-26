namespace WalletPlatform.Application.Reconciliation;

public static class SettlementPostingPolicy
{
    public static bool ShouldSettle(string category) => category == "MATCHED";
}
