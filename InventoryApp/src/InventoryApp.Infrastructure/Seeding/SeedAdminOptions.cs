using InventoryApp.Application.Common.Interfaces;

namespace InventoryApp.Infrastructure.Seeding;

/// <summary>
/// Credentials for the bootstrap administrator. Intentionally has no defaults:
/// there is no fallback value to accidentally ship. Supply via user-secrets
/// (<c>dotnet user-secrets set</c>) or an environment variable.
/// </summary>
public sealed class SeedAdminOptions : IValidatableOptions
{
    public const string SectionName = "SeedAdmin";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }

    /// <summary>True when both credential fields have been supplied.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    /// <summary>
    /// Half-configured credentials are always a mistake, so this is treated as
    /// fatal rather than silently skipped.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var hasEmail = !string.IsNullOrWhiteSpace(Email);
        var hasPassword = !string.IsNullOrWhiteSpace(Password);

        if (hasEmail == hasPassword)
        {
            return [];
        }

        return hasEmail
            ? [$"{SectionName}:Password is required when {SectionName}:Email is set."]
            : [$"{SectionName}:Email is required when {SectionName}:Password is set."];
    }
}
