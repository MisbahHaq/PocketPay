using System.Text.RegularExpressions;

namespace InventoryApp.Domain.Catalog;

/// <summary>
/// The one definition of a valid SKU. It lives in the domain, not in a validator, because the
/// seeder, the request validator and any future import all have to agree on the format and three
/// copies of one regular expression is three chances to drift.
/// </summary>
public static partial class Sku
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 30;

    /// <summary>
    /// Uppercase alphanumeric groups separated by single hyphens, e.g. <c>LED-9W-WARM</c>.
    /// Rejected: leading or trailing hyphens, doubled hyphens and spaces, because all three are
    /// ambiguous to transcribe from a printed label.
    /// </summary>
    [GeneratedRegex(@"^[A-Z0-9]+(?:-[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    public static bool IsValid(string? value) =>
        value is { Length: >= MinimumLength and <= MaximumLength } && Format().IsMatch(value);

    /// <summary>
    /// Uppercases and trims so a user typing "abc-123" is accepted and stored as "ABC-123" rather
    /// than rejected on case alone. Absent input becomes empty, which then fails validation with a
    /// readable message instead of throwing here.
    /// </summary>
    public static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    public const string Description =
        "3 to 30 characters, uppercase letters and digits, optionally split into groups by single hyphens (e.g. LED-9W-WARM).";
}