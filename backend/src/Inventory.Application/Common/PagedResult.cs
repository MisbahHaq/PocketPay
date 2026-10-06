namespace Inventory.Application.Common;

/// <summary>
/// One page of results, plus the metadata a paginated table needs to render itself.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class PagedResult<T>
{
    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total matching rows across all pages.</summary>
    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    /// <summary>Materialises a page from an already-queried sequence. Used by unit tests and in-memory sources.</summary>
    public static PagedResult<T> From(IReadOnlyList<T> source, PagedQuery query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);

        query.Normalize();

        return new PagedResult<T>(
            source.Skip(query.Skip).Take(query.PageSize).ToList(),
            source.Count,
            query.Page,
            query.PageSize);
    }
}