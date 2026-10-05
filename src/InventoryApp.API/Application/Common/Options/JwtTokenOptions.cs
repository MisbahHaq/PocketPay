namespace InventoryApp.Application.Common.Options;

/// <summary>
/// Bound from the "Jwt" configuration section. The signing key is validated on startup so a missing
/// or placeholder secret fails fast instead of quietly signing tokens with a known value.
/// Named JwtTokenOptions rather than TokenOptions to avoid colliding with
/// Microsoft.AspNetCore.Identity.TokenOptions.
/// </summary>
public sealed class JwtTokenOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key. Supplied via user-secrets, never committed.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}