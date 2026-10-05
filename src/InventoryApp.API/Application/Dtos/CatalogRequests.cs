using InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Dtos;

/// <summary>
/// Creates a product. The SKU is set here and never again, which is why no update request carries
/// one - see <see cref="UpdateProductRequest"/>.
/// </summary>
public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    UnitOfMeasure UnitOfMeasure,
    int ReorderLevel,
    decimal CostPrice,
    decimal SalePrice,
    bool IsActive = true);

/// <summary>
/// Replaces a product's mutable fields. Carries the <c>RowVersion</c> it was read at so the write
/// can be rejected if somebody else changed the row in the meantime.
/// </summary>
public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    UnitOfMeasure UnitOfMeasure,
    int ReorderLevel,
    decimal CostPrice,
    decimal SalePrice,
    bool IsActive,
    byte[] RowVersion);

public sealed record CreateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    bool IsActive = true);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    bool IsActive);

/// <summary>
/// Filters, paging and ordering for the product list, bound from the query string.
/// </summary>
/// <remarks>
/// A class with settable properties rather than the positional record used by every other DTO,
/// because <c>[FromQuery]</c> model binding needs a parameterless type whose members it can assign.
/// Keeping it a separate type also lets the paging rules be unit-tested without an HTTP request.
/// </remarks>
public sealed class ProductQuery
{
    public const int DefaultPageSize = 20;

    /// <summary>
    /// A hard ceiling, not a default. Without one, <c>?pageSize=100000</c> is a free denial of
    /// service against the database.
    /// </summary>
    public const int MaximumPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Partial match against name or SKU, case-insensitive.</summary>
    public string? Search { get; init; }

    /// <summary>Also matches products in this category's immediate children, so a parent filter is useful.</summary>
    public Guid? CategoryId { get; init; }

    public bool? IsActive { get; init; }

    public decimal? MinPrice { get; init; }

    /// <summary>Compared against <c>SalePrice</c>, the number a shopper actually sees.</summary>
    public decimal? MaxPrice { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }

    /// <summary>
    /// Clamps values that came straight off the wire. Clamping rather than rejecting keeps a
    /// bookmarked or hand-edited URL working; the effective values are echoed back in the response
    /// so the client can see what it actually got.
    /// </summary>
    public (int Page, int PageSize) Normalize() => (
        Math.Max(Page, 1),
        Math.Clamp(PageSize, 1, MaximumPageSize));
}