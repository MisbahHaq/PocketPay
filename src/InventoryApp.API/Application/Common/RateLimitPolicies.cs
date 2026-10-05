namespace InventoryApp.Application.Common;

/// <summary>Names of the rate limiter policies applied with <c>[EnableRateLimiting]</c>.</summary>
public static class RateLimitPolicies
{
    public const string Auth = nameof(Auth);
}
