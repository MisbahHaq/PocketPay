namespace Inventory.Api;

/// <summary>
/// Named rate-limiting policies.
/// </summary>
/// <remarks>
/// Two layers of brute-force defence, deliberately both:
/// <list type="bullet">
///   <item>Per-IP limits here stop one host from hammering login or refresh at all.</item>
///   <item>ASP.NET Identity's account lockout (5 failures, 15 minutes) stops distributed
///   guesses spread across many addresses.</item>
/// </list>
/// Per-IP alone cannot stop a botnet, and per-account alone lets an attacker lock a
/// real user out of their own system - a denial-of-service in its own right. Together
/// each covers the other's blind spot.
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>Global ceiling for every endpoint, to absorb accidental or malicious floods.</summary>
    public const string Global = "global";

    /// <summary>Strict limit for credential submission.</summary>
    public const string Login = "auth:login";

    /// <summary>Larger allowance for refresh, which every page load may legitimately trigger.</summary>
    public const string Refresh = "auth:refresh";
}