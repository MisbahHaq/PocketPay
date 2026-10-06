using System.ComponentModel.DataAnnotations;
using Inventory.Domain.Identity;

namespace Inventory.Application.Abstractions;

/// <summary>A freshly minted JWT access token and when it stops being valid.</summary>
public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt, string TokenType = "Bearer");

/// <summary>
/// Issues signed JWT access tokens.
/// </summary>
public interface ITokenService
{
    IssuedAccessToken CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);
}

/// <summary>
/// JWT bearer configuration. Bound from the <c>Jwt</c> configuration section.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signing key. Must come from user-secrets, an environment variable or
    /// a vault - never from appsettings.json, which is committed.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Access token lifetime. Deliberately short (default 15 minutes). Access tokens
    /// cannot be revoked, so the blast radius of a stolen one is exactly this number.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token lifetime. Longer because rotation makes each one short-lived in practice.</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}