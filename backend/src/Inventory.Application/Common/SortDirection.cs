namespace Inventory.Application.Common;

/// <summary>Sort direction for list queries. Persisted as a string so it is readable in the query string.</summary>
public enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}