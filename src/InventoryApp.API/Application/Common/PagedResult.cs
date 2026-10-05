namespace InventoryApp.Application.Common;

/// <summary>
/// One page of results plus the counters a client needs to render a pager without a second call.
/// </summary>
/// <typeparam name="T">The projected response type, never an entity.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages)
{
    /// <summary>
    /// Rounding up, so a page of 10 over 25 rows reports 3 pages rather than 2 - the difference
    /// between a pager that works and one that hides the last page.
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize) =>
        new(items, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
}