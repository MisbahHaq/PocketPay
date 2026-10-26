namespace WalletPlatform.Application.Reconciliation;

public static class AutoResolvePolicy
{
    public const string ReviewResolvable = "REVIEW_RESOLVABLE";
    public static bool CanAutoResolve(string category) => category == ReviewResolvable;
}
