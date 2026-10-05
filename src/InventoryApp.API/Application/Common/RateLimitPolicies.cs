namespace InventoryApp.Application.Common;

/// <summary>Names of the rate limiter policies applied with <c>[EnableRateLimiting]</c>.</summary>
public static class RateLimitPolicies
{
    public const string Login = nameof(Login);

    public const string Refresh = nameof(Refresh);
}
