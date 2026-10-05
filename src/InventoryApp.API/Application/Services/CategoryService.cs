using FluentValidation;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Dtos;
using InventoryApp.Application.Validators;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Services;

/// <summary>
/// Category use-cases. The tree is one level deep, which is what keeps the whole taxonomy readable
/// in a single query instead of a recursive walk.
/// </summary>
public sealed class CategoryService(
    IUnitOfWork unitOfWork,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator,
    ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        // One query for the whole taxonomy, projected in SQL.
        //
        // The obvious alternative - read the roots, then loop the children - is the N+1 problem in
        // its purest form: a round trip per category, so a 30-category catalogue costs 31 queries.
        // Projecting the children as a nested collection instead makes EF emit a single statement
        // that builds each parent's child set from the same rows it already read.
        return await unitOfWork.Categories
            .AsNoTracking()
            .Where(category => category.ParentCategoryId == null)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse(
                category.Id,
                category.Name,
                category.Description,
                category.ParentCategoryId,
                category.IsActive,
                // Counted in SQL, so it cannot become N queries.
                category.Products.Count(product => !product.IsDeleted),
                category.Children
                    .OrderBy(child => child.Name)
                    .Select(child => new CategoryResponse(
                        child.Id,
                        child.Name,
                        child.Description,
                        child.ParentCategoryId,
                        child.IsActive,
                        child.Products.Count(product => !product.IsDeleted),
                        null))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await unitOfWork.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new CategoryResponse(
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.ParentCategoryId,
                candidate.IsActive,
                candidate.Products.Count(product => !product.IsDeleted),
                null))
            .FirstOrDefaultAsync(cancellationToken);

        return category ?? throw new NotFoundException($"Category '{id}' was not found.");
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        // Same order as the product service: canonicalise, then validate.
        var normalized = request with { Name = Normalize(request.Name) };

        await createValidator.EnsureValidAsync(normalized, cancellationToken);

        await EnsureNameIsFreeAsync(normalized.Name, excludingId: null, cancellationToken);
        await EnsureParentIsUsableAsync(normalized.ParentCategoryId, cancellationToken);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = normalized.Name,
            Description = normalized.Description?.Trim(),
            ParentCategoryId = normalized.ParentCategoryId,
            IsActive = normalized.IsActive
        };

        unitOfWork.Categories.Add(category);
        await SaveAsync(cancellationToken);

        logger.LogInformation("Category {Name} created.", category.Name);

        return await GetByIdAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var normalized = request with { Name = Normalize(request.Name) };

        await updateValidator.EnsureValidAsync(normalized, cancellationToken);

        var category = await unitOfWork.Categories
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Category '{id}' was not found.");

        await EnsureNameIsFreeAsync(normalized.Name, excludingId: id, cancellationToken);
        await EnsureParentIsUsableAsync(normalized.ParentCategoryId, cancellationToken);

        // Re-parenting a category onto itself would make it its own ancestor.
        if (normalized.ParentCategoryId == id)
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["ParentCategoryId"] = ["A category cannot be its own parent."]
            });

        category.Name = normalized.Name;
        category.Description = normalized.Description?.Trim();
        category.ParentCategoryId = normalized.ParentCategoryId;
        category.IsActive = normalized.IsActive;

        await SaveAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await unitOfWork.Categories
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Category '{id}' was not found.");

        // Subcategories first: a category that is still someone's parent is structurally load-bearing.
        var hasChildren = await unitOfWork.Categories
            .AnyAsync(candidate => candidate.ParentCategoryId == id, cancellationToken);

        if (hasChildren)
            throw new ConflictException(
                $"Category '{category.Name}' still has subcategories. Move or delete them first.");

        // Counts soft-deleted products too, on purpose. The global query filter would hide them and
        // let the delete through, leaving a product whose category no longer exists - and a later
        // restore of that product would fail on a foreign key. A category is therefore only
        // removable once nothing at all points at it, deleted or not.
        var productCount = await unitOfWork.Products
            .IgnoreQueryFilters()
            .CountAsync(product => product.CategoryId == id, cancellationToken);

        if (productCount > 0)
            throw new ConflictException(
                $"Category '{category.Name}' still has {productCount} product(s) filed under it. "
                + "Reassign them first.");

        unitOfWork.Categories.Remove(category);
        await SaveAsync(cancellationToken);

        logger.LogInformation("Category {Name} deleted.", category.Name);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        // Pre-check for a field-named message; the unique index is the actual guarantee.
        var taken = await unitOfWork.Categories
            .AsNoTracking()
            .AnyAsync(
                category => category.Name == name && (excludingId == null || category.Id != excludingId),
                cancellationToken);

        if (taken)
            throw new ConflictException($"A category named '{name}' already exists.");
    }

    /// <summary>
    /// Enforces the one-level rule: a parent must exist and must itself be a root. Without this a
    /// client could nest to any depth and every consumer of the tree would need recursion.
    /// </summary>
    private async Task EnsureParentIsUsableAsync(Guid? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId is not { } parentId)
            return;

        var parent = await unitOfWork.Categories
            .AsNoTracking()
            .Where(category => category.Id == parentId)
            .Select(category => new { category.ParentCategoryId })
            .FirstOrDefaultAsync(cancellationToken);

        if (parent is null)
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["ParentCategoryId"] = [$"Parent category '{parentId}' was not found."]
            });

        if (parent.ParentCategoryId is not null)
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["ParentCategoryId"] = ["Categories may only be nested one level deep."]
            });
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("A category with that name already exists.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // Same provider-neutral backstop as the product service: the API project references only the
        // SQL Server provider, so neither its SqlException number nor SQLite's is nameable from here.
        var message = exception.InnerException?.Message;

        return message is not null
            && (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string name) => name.Trim().ToUpperInvariant();
}