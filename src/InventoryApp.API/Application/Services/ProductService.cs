using FluentValidation;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Dtos;
using InventoryApp.Application.Validators;
using InventoryApp.Domain.Catalog;
using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Services;

/// <summary>
/// Catalogue use-cases. Reads are projected to DTOs inside the query so no list endpoint ever
/// materialises an entity; writes are committed once, here, through <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class ProductService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator,
    TimeProvider timeProvider,
    ILogger<ProductService> logger) : IProductService
{
    /// <summary>
    /// Escape character for the LIKE patterns built in <see cref="ApplyFilters"/>. Backslash is
    /// chosen because it is not a wildcard in either provider, so it cannot collide with one.
    /// </summary>
    private const string LikeEscapeCharacter = "\\";

    public async Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = query.Normalize();

        var sortField = ParseSortField(query.SortBy);
        var descending = ParseDirection(query.SortDirection);

        if (query.MinPrice is { } min && query.MaxPrice is { } max && min > max)
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["MinPrice"] = ["MinPrice cannot be greater than MaxPrice."]
            });

        var costVisible = CanSeeCost();

        // AsNoTracking because a list page is read once and discarded; change tracking would hold
        // every returned row in memory for no benefit. The DbContext's global query filter has
        // already removed soft-deleted products, so none of them are counted or paged over.
        var products = ApplyFilters(unitOfWork.Products.AsNoTracking(), query);

        var totalCount = await products.CountAsync(cancellationToken);

        var ordered = ApplySort(products, sortField, descending);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection(costVisible))
            .ToListAsync(cancellationToken);

        return PagedResult<ProductResponse>.Create(items, totalCount, page, pageSize);
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Filtered before it is projected, not after. Applying a predicate on top of a projection
        // leaves EF Core unable to push it back down to the JOIN, and the query stops translating.
        var product = await unitOfWork.Products
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(Projection(CanSeeCost()))
            .FirstOrDefaultAsync(cancellationToken);

        return product ?? throw new NotFoundException($"Product '{id}' was not found.");
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        // Normalise first, validate second. The reverse order would reject a user who typed the SKU
        // in lower case, which is the single most likely way to trip over the format rule. `with`
        // gives the validator the canonical value without mutating the caller's request.
        var normalized = request with { Sku = Sku.Normalize(request.Sku) };

        await createValidator.EnsureValidAsync(normalized, cancellationToken);

        var sku = normalized.Sku;
        await EnsureSkuIsFreeAsync(sku, cancellationToken);
        await EnsureCategoryExistsAsync(normalized.CategoryId, cancellationToken);

        var product = new Product(sku) { Id = Guid.NewGuid() };

        // Through the entity rather than an object initialiser, so the domain rules on prices and
        // reorder level are the same ones an update would meet.
        product.Update(
            normalized.Name.Trim(),
            normalized.Description?.Trim(),
            normalized.CategoryId,
            normalized.UnitOfMeasure,
            normalized.ReorderLevel,
            normalized.CostPrice,
            normalized.SalePrice,
            normalized.IsActive);

        // The audit fields and the concurrency token are stamped by the DbContext on save, so there
        // is exactly one place that knows who the caller is.
        unitOfWork.Products.Add(product);

        await SaveAsync(cancellationToken);

        logger.LogInformation("Product {Sku} created by {UserId}.", sku, currentUser.UserId);

        return await GetByIdAsync(
            await ResolveCreatedIdAsync(sku, cancellationToken),
            cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);

        // Tracked deliberately: this one is about to be written.
        var product = await unitOfWork.Products
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        // The load-bearing lines, and the subtlest in the module.
        //
        // EF builds the WHERE clause of an update from the *original* value of each concurrency
        // token. Loading the row above set that original to the token as of *this* request - so a
        // plain assignment of the client's token only moves the current value, and the comparison
        // degenerates into "has the row changed in the last few milliseconds", which is always
        // false. A stale client then wins and silently discards the other writer's change.
        //
        // Overwriting the original with what the client actually read is what makes the statement
        // `WHERE Id = @id AND RowVersion = @sent`, so a row somebody else has since touched matches
        // zero rows and SaveAsync turns that into a 409. Routed through IUnitOfWork so the EF
        // Entry API stays behind the abstraction.
        unitOfWork.ExpectRowVersion(product, request.RowVersion);

        // Sku is intentionally absent from the argument list: there is no request field to change it, and
        // the entity has no setter for it either.
        product.Update(
            request.Name.Trim(),
            request.Description?.Trim(),
            request.CategoryId,
            request.UnitOfMeasure,
            request.ReorderLevel,
            request.CostPrice,
            request.SalePrice,
            request.IsActive);

        await SaveAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Products
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");

        // Not a hard delete. Stock movements and invoices will reference this row, so removing it
        // would either break their history or erase the evidence of what once existed.
        product.MarkDeleted(timeProvider.GetUtcNow(), currentUser.UserId);

        await SaveAsync(cancellationToken);

        logger.LogInformation("Product {Sku} soft-deleted by {UserId}.", product.Sku, currentUser.UserId);
    }

    public async Task<ProductResponse> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: the global filter hides soft-deleted rows, and this row is precisely
        // the one that has to be reached.
        var product = await unitOfWork.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");

        // Idempotent, so a double-click on "restore" is a success rather than a confusing error.
        if (product.IsDeleted)
        {
            product.MarkRestored();
            await SaveAsync(cancellationToken);

            logger.LogInformation("Product {Sku} restored by {UserId}.", product.Sku, currentUser.UserId);
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Doubles the characters LIKE treats specially, so a user searching for "50%" matches a literal
    /// percent sign rather than every row.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal);

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> products, ProductQuery query)
    {
        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            // EF.Functions.Like rather than string.Contains, and the difference is not cosmetic:
            // SQL Server compiles Contains to LIKE (case-insensitive under the default collation),
            // but the SQLite provider compiles it to instr(x, y) > 0, which is case-SENSITIVE. The
            // same search box would then quietly behave differently in the integration suite than in
            // production. LIKE is case-insensitive on both providers.
            //
            // The term is escaped so a '%' or '_' typed by a user matches those characters instead
            // of turning into a wildcard, and so a search cannot be widened into a table scan.
            var pattern = $"%{EscapeLikePattern(search)}%";

            products = products.Where(product =>
                EF.Functions.Like(product.Name, pattern, LikeEscapeCharacter)
                || EF.Functions.Like(product.Sku, pattern, LikeEscapeCharacter));
        }

        if (query.CategoryId is { } categoryId)
            // Includes the category's immediate children, so filtering by "Electronics" also returns
            // "LED bulbs" without the caller having to know the tree exists.
            products = products.Where(product =>
                product.CategoryId == categoryId
                || product.Category.ParentCategoryId == categoryId);

        if (query.IsActive is { } isActive)
            products = products.Where(product => product.IsActive == isActive);

        if (query.MinPrice is { } minPrice)
            products = products.Where(product => product.SalePrice >= minPrice);

        if (query.MaxPrice is { } maxPrice)
            products = products.Where(product => product.SalePrice <= maxPrice);

        return products;
    }

    private static IOrderedQueryable<Product> ApplySort(
        IQueryable<Product> products,
        string sortField,
        bool descending)
    {
        IOrderedQueryable<Product> ordered = sortField switch
        {
            ProductSortFields.Sku => descending
                ? products.OrderByDescending(product => product.Sku)
                : products.OrderBy(product => product.Sku),
            // Ordered through a cast to double. SQLite has no decimal type and stores these as TEXT, which
            // orders "100" before "9"; EF refuses the translation outright rather than return an
            // answer that is quietly wrong. The cast is what makes "9" come first. On SQL Server it
            // is a widening conversion on money stored to two decimal places, so it does not change
            // the ordering, and there is no SalePrice index for it to cost an index seek.
            ProductSortFields.Price => descending
                ? products.OrderByDescending(product => (double)product.SalePrice)
                : products.OrderBy(product => (double)product.SalePrice),
            ProductSortFields.CreatedAt => descending
                ? products.OrderByDescending(product => product.CreatedAt)
                : products.OrderBy(product => product.CreatedAt),
            _ => descending
                ? products.OrderByDescending(product => product.Name)
                : products.OrderBy(product => product.Name)
        };

        // Every ordering ends in a unique column. Without one, SQL Server may return equal rows in
        // any order, and paging over a non-deterministic order duplicates rows on one page and drops
        // them from the next.
        return sortField == ProductSortFields.Sku && !descending
            ? ordered.ThenBy(product => product.Name).ThenBy(product => product.Id)
            : ordered.ThenBy(product => product.Sku).ThenBy(product => product.Id);
    }

    /// <summary>
    /// The single SELECT shape for product reads, so the cost redaction cannot be forgotten on one
    /// of them.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<Product, ProductResponse>> Projection(bool costVisible) =>
        product => new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.CategoryId,
            product.Category.Name,
            product.UnitOfMeasure,
            product.ReorderLevel,
            product.SalePrice,
            // The captured flag becomes a query parameter, so the cost column is not even selected
            // for a caller who is not entitled to it.
            costVisible ? product.CostPrice : (decimal?)null,
            product.IsActive,
            product.CreatedAt,
            product.CreatedBy,
            product.UpdatedAt,
            product.UpdatedBy,
            product.IsDeleted,
            product.DeletedAt,
            product.RowVersion);

    /// <summary>
    /// An absent sort is a normal request for the default order, not a malformed one - otherwise
    /// every caller that wants no particular order has to know the default column's name. Only a
    /// value that was actually supplied and is not recognised is a 400.
    /// </summary>
    private static string ParseSortField(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? ProductSortFields.Name
            : ProductSortFields.TryParse(value, out var field)
                ? field
                : throw new ValidationFailedException(new Dictionary<string, string[]>
                {
                    ["SortBy"] = [$"Sort must be one of: {string.Join(", ", ProductSortFields.All)}."]
                });

    private static bool ParseDirection(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? false
            : SortDirections.TryParse(value, out var descending)
                ? descending
                : throw new ValidationFailedException(new Dictionary<string, string[]>
                {
                    ["SortDirection"] = [$"Direction must be one of: {string.Join(", ", SortDirections.All)}."]
                });

    private bool CanSeeCost() =>
        currentUser.Roles.Contains(SystemRoles.Admin) || currentUser.Roles.Contains(SystemRoles.Manager);

    /// <summary>
    /// Looks the created row's id up by its SKU so the response can be built from a projected read,
    /// exactly as a later GET would build it.
    /// </summary>
    private async Task<Guid> ResolveCreatedIdAsync(string sku, CancellationToken cancellationToken) =>
        await unitOfWork.Products
            .Where(product => product.Sku == sku)
            .Select(product => product.Id)
            .SingleAsync(cancellationToken);

    private async Task EnsureSkuIsFreeAsync(string sku, CancellationToken cancellationToken)
    {
        // A pre-check, so the caller gets a message that names the offending field. It is not the
        // guarantee: two simultaneous requests can both pass it. The unique index is the guarantee,
        // and SaveAsync turns the database's complaint into the same 409.
        var taken = await unitOfWork.Products
            .AsNoTracking()
            .AnyAsync(product => product.Sku == sku, cancellationToken);

        if (taken)
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        // IsActive is deliberately not checked: a product must stay editable after its category is
        // retired, otherwise the row would freeze the day somebody hid the category.
        var exists = await unitOfWork.Categories
            .AsNoTracking()
            .AnyAsync(category => category.Id == categoryId, cancellationToken);

        if (!exists)
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["CategoryId"] = [$"Category '{categoryId}' was not found."]
            });
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row version moved between read and write. Translated here rather than in the
            // exception middleware so this layer owns the wording and no EF exception escapes it.
            throw new ConflictException(
                "This product was modified by someone else while you were editing it. "
                + "Reload it and apply your changes again.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("A product with that SKU already exists.");
        }
    }

    /// <summary>
    /// Recognises a unique-index violation without naming a provider type.
    /// </summary>
    /// <remarks>
    /// SQL Server reports 2601/2627 on <c>SqlException.Number</c> and SQLite reports 19 on
    /// <c>SqliteException.SqliteErrorCode</c>, but this project references only the SQL Server
    /// provider, so the SQLite type is not nameable here and the tests run on SQLite. The message
    /// text is the one detail both providers agree on, so this is a backstop behind the pre-check
    /// rather than the primary defence - which is why it can afford to be approximate.
    /// </remarks>
    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message;

        return message is not null
            && (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }
}