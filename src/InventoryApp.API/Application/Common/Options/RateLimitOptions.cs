namespace InventoryApp.Application.Common.Options;

/// <summary>
/// Bound from the "RateLimiting" section. Limits are configuration rather than constants so a
/// tighter value can be applied per environment without a redeploy of compiled code.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per window allowed on /login.</summary>
    public int LoginPermitLimit { get; set; } = 10;

    /// <summary>
    /// Requests per window allowed on /refresh. Higher than the login budget because legitimate
    /// clients refresh on a timer, whereas login volume is tightly coupled to human activity.
    /// </summary>
    public int RefreshPermitLimit { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}
