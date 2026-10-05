using InventoryApp.Application.Common.Interfaces;

namespace InventoryApp.Application.Auth;

/// <summary>
/// Refresh-token settings owned by the application layer (the layer that
/// enforces the rules), bound from configuration by the infrastructure layer.
/// </summary>
public sealed class RefreshTokenOptions : IValidatableOptions
{
    public const string SectionName = "RefreshTokens";

    /// <summary>Seven days, per the security requirements.</summary>
    public int LifetimeDays { get; set; } = 7;

    public TimeSpan Lifetime => TimeSpan.FromDays(LifetimeDays);

    public IReadOnlyList<string> Validate() =>
        LifetimeDays is < 1 or > 365
            ? [$"{SectionName}:LifetimeDays must be between 1 and 365 (was {LifetimeDays})."]
            : [];
}
