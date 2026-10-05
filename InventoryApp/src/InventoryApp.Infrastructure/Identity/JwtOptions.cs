using System.Text;
using InventoryApp.Application.Common.Interfaces;

namespace InventoryApp.Infrastructure.Identity;

/// <summary>
/// Signing and validation settings for access tokens. Values come from
/// configuration / user-secrets - never from source control.
/// </summary>
public sealed class JwtOptions : IValidatableOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length enforced by the HMAC-SHA256 signing algorithm.</summary>
    public const int MinimumSigningKeyLengthInBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Symmetric signing secret. Supplied via user-secrets or an environment
    /// variable; appsettings.json only contains a placeholder.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Tolerance applied to token expiry. Zero by default: a token that has just
    /// expired should be rejected, and the clocks are already synchronised.
    /// </summary>
    public int ClockSkewSeconds { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            problems.Add($"{SectionName}:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            problems.Add($"{SectionName}:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            problems.Add(
                $"{SectionName}:SigningKey is required. Set it with: "
                + "dotnet user-secrets set \"Jwt:SigningKey\" \"<at-least-32-random-characters>\"");
        }
        else if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumSigningKeyLengthInBytes)
        {
            problems.Add(
                $"{SectionName}:SigningKey must be at least {MinimumSigningKeyLengthInBytes} bytes "
                + "long for HMAC-SHA256.");
        }

        if (AccessTokenLifetimeMinutes is < 1 or > 1440)
        {
            problems.Add(
                $"{SectionName}:AccessTokenLifetimeMinutes must be between 1 and 1440 "
                + $"(was {AccessTokenLifetimeMinutes}).");
        }

        if (ClockSkewSeconds is < 0 or > 300)
        {
            problems.Add(
                $"{SectionName}:ClockSkewSeconds must be between 0 and 300 (was {ClockSkewSeconds}).");
        }

        return problems;
    }
}
