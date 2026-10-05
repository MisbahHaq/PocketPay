using InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Dtos;

/// <summary>
/// A product as returned by the API. Projected straight from the database in the query, so nothing
/// here is an entity and there is no lazy loading to trip over.
/// </summary>
/// <remarks>
/// <c>CostPrice</c> is nullable and null for callers who are not allowed to see it. Margin is
/// commercially sensitive, and Staff need only the price a shopper pays. Redacting it in the
/// projection means the column never even reaches the client rather than being filtered out after
/// it has been sent.
/// </remarks>
public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    UnitOfMeasure UnitOfMeasure,
    int ReorderLevel,
    decimal SalePrice,
    decimal? CostPrice,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    byte[] RowVersion);

/// <summary>
/// A category plus its immediate children, so a UI can render the whole taxonomy in one call.
/// </summary>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    bool IsActive,
    int ProductCount,
    IReadOnlyList<CategoryResponse>? Children = null);