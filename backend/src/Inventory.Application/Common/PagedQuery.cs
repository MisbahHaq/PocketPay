namespace Inventory.Application.Common;

/// <summary>
/// Base shape for every list query.
/// </summary>
/// <remarks>
/// <para>
/// The same contract for all fifteen-plus list endpoints, so the frontend can build
/// one reusable table component instead of fifteen. Every list view in this system is
/// server-paged: an inventory catalogue in a real warehouse runs to hundreds of
/// thousands of SKUs, and shipping those to the browser would be unusable.
/// </para>
/// <para>
/// <see cref="SortBy"/> is deliberately a free-form string rather than an enum. It is
/// <b>never</b> interpolated into an expression tree; each query whitelists the
/// columns it accepts and ignores anything else (see
/// <c>ProductSorting</c>). Passing a column name to a dynamic LINQ provider instead
/// would let any caller trigger an arbitrary column scan or a SQL error.
/// </para>
/// <para>
/// Mutable with public setters so model binding can populate it from the query
/// string. Call <see cref="Normalize"/> before using the values.
/// </para>
/// </remarks>
public class PagedQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Whitelisted column name, or null for the query's default ordering.</summary>
    public string? SortBy { get; set; }

    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

    /// <summary>Free-text search. Matching is always case-insensitive and always indexed where possible.</summary>
    public string? Search { get; set; }

    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Clamps hostile or accidental values into a range the database can serve
    /// efficiently. A caller asking for <c>?pageSize=100000</c> should get the
    /// default page, not an attempt to materialise the whole table.
    /// </summary>
    public PagedQuery Normalize()
    {
        if (Page < 1)
        {
            Page = 1;
        }

        PageSize = PageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => PageSize,
        };

        if (!SortDirection.IsDefined(SortDirection))
        {
            SortDirection = SortDirection.Ascending;
        }

        return this;
    }

    /// <summary>True when the caller supplied a search term with actual content.</summary>
    public bool HasSearch => !string.IsNullOrWhiteSpace(Search);
}