namespace InventoryApp.API.RateLimiting;

/// <summary>Rate limit policy names, referenced from attributes via constants.</summary>
public static class RateLimitPolicyNames
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
}
