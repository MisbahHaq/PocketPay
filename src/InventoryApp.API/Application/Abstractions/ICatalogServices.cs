using InventoryApp.Application.Common;
using InventoryApp.Application.Dtos;

namespace InventoryApp.Application.Abstractions;

/// <summary>
/// Catalogue use-cases. Controllers depend on this, never on EF Core, so the persistence choice
/// stays replaceable and every read is shaped for the caller's needs rather than the table's.
/// </summary>
public interface IProductService
{
    Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Applies <paramref name="request"/> to the product. Throws <c>ConflictException</c> when the
    /// row has changed since the request was composed.
    /// </summary>
    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: the row is flagged and stays queryable by an administrator.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

/// <summary>Undoes a soft delete, which is why it is a separate permission from update.</summary>
Task<ProductResponse> RestoreAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Category use-cases: the one-level taxonomy that products are filed under.</summary>
public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);

    /// <summary>Hard delete. Refused while anything still points at the category.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}