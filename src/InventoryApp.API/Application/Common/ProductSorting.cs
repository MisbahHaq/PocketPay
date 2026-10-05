namespace InventoryApp.Application.Common;

/// <summary>
/// The columns a product list may be ordered by. A closed list rather than a free-text
/// <c>ORDER BY</c> parameter: the value is compared against this set before it reaches the query,
/// so an unrecognised column is a 400 instead of a database error or an injection seam.
/// </summary>
public static class ProductSortFields
{
    public const string Name = "name";
    public const string Sku = "sku";
    public const string Price = "price";
    public const string CreatedAt = "createdAt";

    public static readonly IReadOnlyList<string> All = [Name, Sku, Price, CreatedAt];

    /// <summary>Case-insensitive so <c>?sortBy=SKU</c> behaves the same as <c>?sortBy=sku</c>.</summary>
    public static bool TryParse(string? value, out string field)
    {
        var match = All.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        field = match ?? Name;
        return match is not null;
    }
}

public static class SortDirections
{
    public const string Ascending = "asc";
    public const string Descending = "desc";

    public static readonly IReadOnlyList<string> All = [Ascending, Descending];

    public static bool TryParse(string? value, out bool descending)
    {
        var match = All.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        descending = string.Equals(match, Descending, StringComparison.Ordinal);
        return match is not null;
    }
}