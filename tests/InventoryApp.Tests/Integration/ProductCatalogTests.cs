using System.Globalization;
using System.Net;
using InventoryApp.Application.Common;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Enums;
using InventoryApp.Domain.Security;
using InventoryApp.Infrastructure.Persistence;
using InventoryApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApp.Tests.Integration;

/// <summary>
/// The catalogue's behaviour end to end, through the real pipeline: routing, authorization, model
/// binding, validation, EF Core against a relational database, and the exception handler.
/// </summary>
/// <remarks>
/// Each test creates its own category, so tests do not depend on the order xUnit runs them in or on
/// the sample catalogue - which the seeder correctly refuses to create for the test environment.
/// </remarks>
public class ProductCatalogTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_product_can_be_created_read_and_updated()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var created = await _client.CreateProductAsync(token, category.Id, name: "Cordless Drill");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Cordless Drill", created.Name);
        Assert.Equal(category.Id, created.CategoryId);
        Assert.Equal(category.Name, created.CategoryName);
        Assert.Equal(19.99m, created.SalePrice);
        Assert.True(created.IsActive);
        Assert.False(created.IsDeleted);
        Assert.NotNull(created.RowVersion);
        Assert.NotEmpty(created.RowVersion!);

        var fetched = await _client.GetAsync($"{CatalogClient.ProductsPath}/{created.Id}", token, None);
        await fetched.EnsureOkAsync();
        Assert.Equal(created.Id, (await fetched.ReadAsync<ProductResponse>()).Id);

        var update = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{created.Id}",
            new UpdateProductRequest(
                "Cordless Drill Mk II", "Now with two batteries.", category.Id,
                UnitOfMeasure.Set, 5, 12.50m, 29.95m, true, created.RowVersion!),
            token);

        await update.EnsureOkAsync();
        var updated = await update.ReadAsync<ProductResponse>();

        Assert.Equal("Cordless Drill Mk II", updated.Name);
        Assert.Equal(29.95m, updated.SalePrice);
        Assert.Equal(UnitOfMeasure.Set, updated.UnitOfMeasure);
    }

    [Fact]
    public async Task The_sku_is_normalised_on_the_way_in()
    {
        // Typing lower case is accepted rather than refused, and stored upper case.
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        var created = await _client.CreateProductAsync(token, category.Id, sku: "drill-18v-x2");

        Assert.Equal("DRILL-18V-X2", created.Sku);
    }

    [Fact]
    public async Task An_update_cannot_change_the_sku()
    {
        // There is no Sku on UpdateProductRequest, so the only thing to assert is that a payload
        // carrying one anyway leaves the stored SKU untouched.
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var created = await _client.CreateProductAsync(token, category.Id, sku: "FIXED-SKU-1");

        var response = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{created.Id}",
            new
            {
                name = "Renamed",
                description = (string?)null,
                categoryId = category.Id,
                unitOfMeasure = UnitOfMeasure.Piece,
                reorderLevel = 0,
                costPrice = 1m,
                salePrice = 2m,
                isActive = true,
                rowVersion = created.RowVersion,
                sku = "ATTACKER-SKU"      // ignored: no matching member on the DTO
            },
            token);

        await response.EnsureOkAsync();
        Assert.Equal("FIXED-SKU-1", (await response.ReadAsync<ProductResponse>()).Sku);
    }

    [Fact]
    public async Task A_duplicate_sku_is_rejected_with_a_409()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var sku = $"DUP-{Guid.NewGuid():N}"[..16];

        await _client.CreateProductAsync(token, category.Id, sku: sku);

        var response = await _client.PostAsAsync(
            CatalogClient.ProductsPath,
            new CreateProductRequest(sku, "Copycat", null, category.Id, UnitOfMeasure.Piece, 0, 1m, 2m),
            token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.IsProblemDetails());

        var problem = await response.ReadProblemAsync();
        Assert.Contains(sku, problem.GetProperty("detail").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_duplicate_category_name_is_rejected_with_a_409()
    {
        var token = await _client.TokenAsync();
        var name = $"DUP-CAT-{Guid.NewGuid():N}"[..20];

        await _client.CreateCategoryAsync(token, name);
        var response = await _client.PostAsAsync(
            CatalogClient.CategoriesPath,
            new CreateCategoryRequest(name, null, null),
            token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.IsProblemDetails());
    }

    [Fact]
    public async Task Selling_below_cost_is_rejected_with_a_400_naming_the_field()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        var response = await _client.PostAsAsync(
            CatalogClient.ProductsPath,
            new CreateProductRequest($"LOSS-{Guid.NewGuid():N}"[..14], "Loss leader", null,
                category.Id, UnitOfMeasure.Piece, 0, CostPrice: 10m, SalePrice: 5m),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadProblemAsync();
        Assert.True(problem.GetProperty("errors").TryGetProperty("SalePrice", out _));
    }

    [Fact]
    public async Task An_unknown_product_id_is_a_404()
    {
        var token = await _client.TokenAsync();

        var response = await _client.GetAsync($"{CatalogClient.ProductsPath}/{Guid.NewGuid()}", token, None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.IsProblemDetails());
        Assert.Equal(404, (await response.ReadProblemAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Search_matches_name_or_sku_partially_and_ignores_case()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        // Unique substrings, so the assertion does not depend on what else exists in the shared database.
        var drill = await _client.CreateProductAsync(token, category.Id, name: "Cordless Zylophordrill", sku: "DRL-771");
        await _client.CreateProductAsync(token, category.Id, name: "Claw Zylophammer", sku: "HAM-772");

        // Lower case against a mixed-case name: the search has to be case-insensitive, and that
        // holds on SQL Server and SQLite alike.
        var byName = await ReadPageAsync(token, search: "zylophordrill");
        Assert.Single(byName.Items);
        Assert.Equal(drill.Id, byName.Items[0].Id);

        var bySku = await ReadPageAsync(token, search: "ham-77");
        Assert.Single(bySku.Items);
        Assert.Equal("HAM-772", bySku.Items[0].Sku);

        // A term matching neither proves the search is not simply returning everything.
        Assert.Empty((await ReadPageAsync(token, search: "no-such-thing")).Items);
    }

    [Fact]
    public async Task A_search_term_containing_a_wildcard_is_taken_literally()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        await _client.CreateProductAsync(token, category.Id, name: "Fifty percent widget", sku: "PCT-001");

        // "%" is LIKE's single-character wildcard. Left unescaped this would match every product in
        // the database; escaped, it looks for a literal percent sign and finds only the one.
        var result = await ReadPageAsync(token, categoryId: category.Id, search: "50%");

        Assert.Empty(result.Items);
        Assert.Equal(1m, (await ReadPageAsync(token, categoryId: category.Id, search: "percent")).TotalCount);
    }

    [Fact]
    public async Task Filtering_by_category_includes_the_child_category()
    {
        var token = await _client.TokenAsync();
        var parent = await _client.CreateCategoryAsync(token);
        var child = await _client.CreateCategoryAsync(token, parentCategoryId: parent.Id);

        var inParent = await _client.CreateProductAsync(token, parent.Id, name: "Parent item");
        var inChild = await _client.CreateProductAsync(token, child.Id, name: "Child item");
        var elsewhere = await _client.CreateProductAsync(
            token,
            (await _client.CreateCategoryAsync(token)).Id,
            name: "Unrelated");

        var result = await ReadPageAsync(token, categoryId: parent.Id);
        var ids = result.Items.Select(item => item.Id).ToArray();

        Assert.Contains(inParent.Id, ids);
        Assert.Contains(inChild.Id, ids);
        Assert.DoesNotContain(elsewhere.Id, ids);
    }

    [Fact]
    public async Task Filtering_by_price_uses_the_sale_price()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        var cheap = await _client.CreateProductAsync(token, category.Id, name: "Cheap", salePrice: 5m, costPrice: 1m);
        await _client.CreateProductAsync(token, category.Id, name: "Pricey", salePrice: 50m, costPrice: 40m);

        // Scoped to this test's own category: the suite shares one database, so a bare price range would
        // also pick up rows other tests happened to price inside it.
        var filtered = await ReadPageAsync(token, categoryId: category.Id, minPrice: 10m, maxPrice: 100m);

        Assert.DoesNotContain(cheap.Id, filtered.Items.Select(item => item.Id));
        Assert.Equal(50m, filtered.Items.Single().SalePrice);
    }

    [Fact]
    public async Task Filtering_by_is_active_returns_only_that_set()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        var active = await _client.CreateProductAsync(token, category.Id, name: "Live", isActive: true);
        await _client.CreateProductAsync(token, category.Id, name: "Retired", isActive: false);

        var activeResult = await ReadPageAsync(token, isActive: true);
        Assert.Contains(active.Id, activeResult.Items.Select(item => item.Id));

        var inactiveResult = await ReadPageAsync(token, isActive: false);
        Assert.DoesNotContain(active.Id, inactiveResult.Items.Select(item => item.Id));
        Assert.Contains("Retired", inactiveResult.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Sorting_honours_the_requested_column_and_direction()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        await _client.CreateProductAsync(token, category.Id, name: "Banana", sku: "AAA-001", salePrice: 30m, costPrice: 20m);
        await _client.CreateProductAsync(token, category.Id, name: "Apple", sku: "ZZZ-999", salePrice: 10m, costPrice: 5m);
        await _client.CreateProductAsync(token, category.Id, name: "Cherry", sku: "MMM-500", salePrice: 20m, costPrice: 10m);

        // Scoped to this test's own category. The suite shares one database, so an unscoped read would
        // also return rows other tests created and the expected ordering would be meaningless.
        var byNameAsc = await ReadPageAsync(token, categoryId: category.Id, sortBy: "name", direction: "asc");
        Assert.Equal(["Apple", "Banana", "Cherry"], byNameAsc.Items.Select(item => item.Name));

        var byNameDesc = await ReadPageAsync(token, categoryId: category.Id, sortBy: "name", direction: "desc");
        Assert.Equal(["Cherry", "Banana", "Apple"], byNameDesc.Items.Select(item => item.Name));

        var bySkuAsc = await ReadPageAsync(token, categoryId: category.Id, sortBy: "sku", direction: "asc");
        Assert.Equal(["AAA-001", "MMM-500", "ZZZ-999"], bySkuAsc.Items.Select(item => item.Sku));

        var byPriceDesc = await ReadPageAsync(token, categoryId: category.Id, sortBy: "price", direction: "desc");
        Assert.Equal([30m, 20m, 10m], byPriceDesc.Items.Select(item => item.SalePrice));
    }

    [Fact]
    public async Task An_unknown_sort_column_is_rejected_with_a_400()
    {
        // The value never reaches the database: it is checked against a closed list first.
        var token = await _client.TokenAsync();

        var response = await _client.GetAsync($"{CatalogClient.ProductsPath}?sortBy=id;drop", token, None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemDetails());
    }

    [Fact]
    public async Task An_unknown_direction_is_rejected_with_a_400()
    {
        var token = await _client.TokenAsync();

        var response = await _client.GetAsync($"{CatalogClient.ProductsPath}?sortDirection=sideways", token, None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paging_splits_the_results_and_reports_the_totals()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        for (var index = 0; index < 7; index++)
            await _client.CreateProductAsync(token, category.Id, name: $"Paged {index:D2}");

        var pageOne = await ReadPageAsync(token, categoryId: category.Id, page: 1, pageSize: 3);
        var pageTwo = await ReadPageAsync(token, categoryId: category.Id, page: 2, pageSize: 3);
        var pageThree = await ReadPageAsync(token, categoryId: category.Id, page: 3, pageSize: 3);

        Assert.Equal(7, pageOne.TotalCount);
        Assert.Equal(3, pageOne.TotalPages);
        Assert.Equal(1, pageOne.Page);
        Assert.Equal(3, pageOne.PageSize);
        Assert.Equal(3, pageOne.Items.Count);
        Assert.Single(pageThree.Items);

        // No row appears twice and none is lost between pages.
        var seen = pageOne.Items.Concat(pageTwo.Items).Concat(pageThree.Items).Select(item => item.Id).ToHashSet();
        Assert.Equal(7, seen.Count);
    }

    [Fact]
    public async Task Page_size_is_clamped_rather_than_refused()
    {
        var token = await _client.TokenAsync();

        var response = await _client.GetAsync($"{CatalogClient.ProductsPath}?pageSize=100000", token, None);
        await response.EnsureOkAsync();

        Assert.Equal(ProductQuery.MaximumPageSize, (await response.ReadAsync<PagedResult<ProductResponse>>()).PageSize);
    }

    [Fact]
    public async Task A_stale_row_version_is_rejected_with_a_409()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id, name: "Contended");

        // Two clients read the same row.
        var first = await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None);
        var second = await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None);
        var firstView = await first.ReadAsync<ProductResponse>();
        var secondView = await second.ReadAsync<ProductResponse>();

        UpdateProductRequest Update(string name) =>
            new(name, null, category.Id, UnitOfMeasure.Piece, 0, 1m, 2m, true, firstView.RowVersion!);

        var firstWrite = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}", Update("First writer"), token);
        await firstWrite.EnsureOkAsync();

        // The second client still holds the pre-update token, so its write must be refused rather
        // than quietly overwriting the first.
        var secondWrite = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}",
            new UpdateProductRequest("Second writer", null, category.Id, UnitOfMeasure.Piece, 0, 1m, 3m, true, secondView.RowVersion!),
            token);

        Assert.Equal(HttpStatusCode.Conflict, secondWrite.StatusCode);
        Assert.True(secondWrite.IsProblemDetails());

        var problem = await secondWrite.ReadProblemAsync();
        Assert.Contains("modified by someone else", problem.GetProperty("detail").GetString()!);

        // And the first writer's value is the one that survived.
        var final = await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None);
        Assert.Equal("First writer", (await final.ReadAsync<ProductResponse>()).Name);
    }

    [Fact]
    public async Task A_fresh_row_version_is_accepted_after_a_conflict()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id);

        var first = await (await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None))
            .ReadAsync<ProductResponse>();

        await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}",
            new UpdateProductRequest("V1", null, category.Id, UnitOfMeasure.Piece, 0, 1m, 2m, true, first.RowVersion!),
            token);

        // Reload, then retry: the client's recovery path.
        var reloaded = await (await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None))
            .ReadAsync<ProductResponse>();

        var retry = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}",
            new UpdateProductRequest("V2", null, category.Id, UnitOfMeasure.Piece, 0, 1m, 4m, true, reloaded.RowVersion!),
            token);

        await retry.EnsureOkAsync();
        Assert.Equal("V2", (await retry.ReadAsync<ProductResponse>()).Name);
    }

    [Fact]
    public async Task Deleting_a_product_hides_it_without_removing_it()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id, name: "Doomed");

        var deleted = await _client.DeleteAsAsync($"{CatalogClient.ProductsPath}/{product.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Gone from the list...
        var list = await ReadPageAsync(token, categoryId: category.Id);
        Assert.DoesNotContain(product.Id, list.Items.Select(item => item.Id));

        // ...and gone from a direct read.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None)).StatusCode);

        // ...but still physically present, which is the whole point of a soft delete.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == product.Id));
    }

    [Fact]
    public async Task Restoring_a_product_brings_it_back()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id, name: "Resurrected");

        await _client.DeleteAsAsync($"{CatalogClient.ProductsPath}/{product.Id}", token);

        var restored = await _client.PostAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}/restore", new { }, token);

        await restored.EnsureOkAsync();
        var body = await restored.ReadAsync<ProductResponse>();
        Assert.False(body.IsDeleted);
        Assert.Null(body.DeletedAt);

        var list = await ReadPageAsync(token, categoryId: category.Id);
        Assert.Contains(product.Id, list.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Restoring_a_product_that_was_never_deleted_is_a_no_op()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id);

        var restored = await _client.PostAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}/restore", new { }, token);

        await restored.EnsureOkAsync();
        Assert.False((await restored.ReadAsync<ProductResponse>()).IsDeleted);
    }

    [Fact]
    public async Task The_audit_fields_record_who_created_and_updated_the_row()
    {
        var token = await _client.TokenAsync();
        var manager = await _client.RegisterAndLoginAsync(SystemRoles.Manager);
        var category = await _client.CreateCategoryAsync(token);

        var product = await _client.CreateProductAsync(token, category.Id, name: "Audited");
        Assert.False(string.IsNullOrEmpty(product.CreatedBy));
        Assert.Equal(product.CreatedAt, product.UpdatedAt);

        var updated = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}",
            new UpdateProductRequest("Audited and changed", null, category.Id,
                UnitOfMeasure.Piece, 0, 1m, 2m, true, product.RowVersion!),
            manager.AccessToken);

        await updated.EnsureOkAsync();
        var body = await updated.ReadAsync<ProductResponse>();

        Assert.True(body.UpdatedAt >= product.UpdatedAt);
        Assert.NotEqual(product.CreatedBy, body.UpdatedBy);
    }

    [Fact]
    public async Task A_category_cannot_be_deleted_while_it_still_has_products()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        await _client.CreateProductAsync(token, category.Id);

        var response = await _client.DeleteAsAsync($"{CatalogClient.CategoriesPath}/{category.Id}", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("product", (await response.ReadProblemAsync()).GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task A_category_cannot_be_deleted_while_a_soft_deleted_product_still_points_at_it()
    {
        // The subtle case: the global filter would hide the deleted product and let the category go,
        // leaving a product whose category no longer exists - so its later restore would fail.
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id);

        await _client.DeleteAsAsync($"{CatalogClient.ProductsPath}/{product.Id}", token);

        var response = await _client.DeleteAsAsync($"{CatalogClient.CategoriesPath}/{category.Id}", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_category_cannot_be_deleted_while_it_has_subcategories()
    {
        var token = await _client.TokenAsync();
        var parent = await _client.CreateCategoryAsync(token);
        await _client.CreateCategoryAsync(token, parentCategoryId: parent.Id);

        var response = await _client.DeleteAsAsync($"{CatalogClient.CategoriesPath}/{parent.Id}", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("subcategories", (await response.ReadProblemAsync()).GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task An_empty_leaf_category_can_be_deleted()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);

        var response = await _client.DeleteAsAsync($"{CatalogClient.CategoriesPath}/{category.Id}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"{CatalogClient.CategoriesPath}/{category.Id}", token, None)).StatusCode);
    }

    [Fact]
    public async Task The_category_list_returns_the_whole_tree_in_one_call_with_product_counts()
    {
        var token = await _client.TokenAsync();
        var parent = await _client.CreateCategoryAsync(token);
        var child = await _client.CreateCategoryAsync(token, parentCategoryId: parent.Id);

        await _client.CreateProductAsync(token, parent.Id);
        await _client.CreateProductAsync(token, child.Id);

        var response = await _client.GetAsync(CatalogClient.CategoriesPath, token, None);
        await response.EnsureOkAsync();
        var all = await response.ReadAsync<List<CategoryResponse>>();

        var root = all.Single(candidate => candidate.Id == parent.Id);
        Assert.Equal(1, root.ProductCount);
        Assert.NotNull(root.Children);
        Assert.Equal(child.Id, root.Children!.Single().Id);
        Assert.Equal(1, root.Children.Single().ProductCount);
    }

    [Fact]
    public async Task Categories_may_only_be_nested_one_level_deep()
    {
        var token = await _client.TokenAsync();
        var root = await _client.CreateCategoryAsync(token);
        var child = await _client.CreateCategoryAsync(token, parentCategoryId: root.Id);

        var response = await _client.PostAsAsync(
            CatalogClient.CategoriesPath,
            new CreateCategoryRequest("TOO-DEEP", null, child.Id),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "one level",
            (await response.ReadProblemAsync()).GetProperty("errors").GetProperty("ParentCategoryId")[0].GetString()!);
    }

    [Fact]
    public async Task A_product_cannot_be_filed_under_a_category_that_does_not_exist()
    {
        var token = await _client.TokenAsync();

        var response = await _client.PostAsAsync(
            CatalogClient.ProductsPath,
            new CreateProductRequest($"ORPHAN-{Guid.NewGuid():N}"[..16], "Orphan", null,
                Guid.NewGuid(), UnitOfMeasure.Piece, 0, 1m, 2m),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).GetProperty("errors").TryGetProperty("CategoryId", out _));
    }

    [Fact]
    public async Task Staff_cannot_see_cost_price_but_manager_and_admin_can()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        var product = await _client.CreateProductAsync(token, category.Id, costPrice: 7.50m, salePrice: 12m);

        var staff = await _client.RegisterAndLoginAsync(SystemRoles.Staff);
        var manager = await _client.RegisterAndLoginAsync(SystemRoles.Manager);

        var staffView = await (await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", staff.AccessToken, None))
            .ReadAsync<ProductResponse>();
        Assert.Null(staffView.CostPrice);
        Assert.Equal(12m, staffView.SalePrice);

        var managerView = await (await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", manager.AccessToken, None))
            .ReadAsync<ProductResponse>();
        Assert.Equal(7.50m, managerView.CostPrice);

        var adminView = await (await _client.GetAsync($"{CatalogClient.ProductsPath}/{product.Id}", token, None))
            .ReadAsync<ProductResponse>();
        Assert.Equal(7.50m, adminView.CostPrice);
    }

    [Fact]
    public async Task Staff_do_not_see_cost_price_in_the_list_either()
    {
        var token = await _client.TokenAsync();
        var category = await _client.CreateCategoryAsync(token);
        await _client.CreateProductAsync(token, category.Id, costPrice: 3.25m, salePrice: 8m);

        var staff = await _client.RegisterAndLoginAsync(SystemRoles.Staff);

        var response = await _client.GetAsync($"{CatalogClient.ProductsPath}?categoryId={category.Id}", staff.AccessToken, None);
        await response.EnsureOkAsync();
        var page = await response.ReadAsync<PagedResult<ProductResponse>>();

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Null(item.CostPrice));
    }

    private async Task<PagedResult<ProductResponse>> ReadPageAsync(
        string token,
        string? search = null,
        Guid? categoryId = null,
        bool? isActive = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null,
        string? direction = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };

        if (search is not null) query.Add($"search={Uri.EscapeDataString(search)}");
        if (categoryId is not null) query.Add($"categoryId={categoryId}");
        if (isActive is not null) query.Add($"isActive={(isActive.Value ? "true" : "false")}");
        if (minPrice is not null) query.Add($"minPrice={minPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        if (maxPrice is not null) query.Add($"maxPrice={maxPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        if (sortBy is not null) query.Add($"sortBy={sortBy}");
        if (direction is not null) query.Add($"sortDirection={direction}");

        var response = await _client.GetAsync(
            $"{CatalogClient.ProductsPath}?{string.Join('&', query)}", token, None);

        await response.EnsureOkAsync();
        return await response.ReadAsync<PagedResult<ProductResponse>>();
    }
}