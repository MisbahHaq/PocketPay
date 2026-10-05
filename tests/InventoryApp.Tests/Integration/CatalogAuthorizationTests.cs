using System.Net;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Enums;
using InventoryApp.Domain.Security;
using InventoryApp.Tests.Support;

namespace InventoryApp.Tests.Integration;

/// <summary>
/// The catalogue's role matrix, checked against the real catalogue endpoints rather than the
/// placeholder sample ones Module 1 used. 403 is the interesting value: the token worked, the role
/// did not permit the call.
/// </summary>
public class CatalogAuthorizationTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// A well-formed body, so the unauthenticated write is rejected for the missing token rather
    /// than for its content.
    /// </summary>
    private static readonly object EmptyPayload = new { name = "anonymous" };

    public static TheoryData<string, string, string, HttpStatusCode> RoleMatrix => new()
    {
        // Anyone signed in may read the catalogue: that is what Staff are for.
        { SystemRoles.Staff, "GET", CatalogClient.ProductsPath, HttpStatusCode.OK },
        { SystemRoles.Manager, "GET", CatalogClient.ProductsPath, HttpStatusCode.OK },
        { SystemRoles.Admin, "GET", CatalogClient.ProductsPath, HttpStatusCode.OK },
        { SystemRoles.Staff, "GET", CatalogClient.CategoriesPath, HttpStatusCode.OK },
        { SystemRoles.Manager, "GET", CatalogClient.CategoriesPath, HttpStatusCode.OK },
        { SystemRoles.Admin, "GET", CatalogClient.CategoriesPath, HttpStatusCode.OK },

        // Authoring is Manager and Admin.
        { SystemRoles.Staff, "POST", CatalogClient.ProductsPath, HttpStatusCode.Forbidden },
        { SystemRoles.Manager, "POST", CatalogClient.ProductsPath, HttpStatusCode.Created },
        { SystemRoles.Admin, "POST", CatalogClient.ProductsPath, HttpStatusCode.Created },
        { SystemRoles.Staff, "POST", CatalogClient.CategoriesPath, HttpStatusCode.Forbidden },
        { SystemRoles.Manager, "POST", CatalogClient.CategoriesPath, HttpStatusCode.Created },
        { SystemRoles.Admin, "POST", CatalogClient.CategoriesPath, HttpStatusCode.Created },

        // Removal, and undoing a removal, are Admin only. Category deletion sits on the delete
        // policy too rather than the edit one: a category that disappears takes its whole subtree
        // of products out of the catalogue with it.
        { SystemRoles.Staff, "DELETE", CatalogClient.ProductsPath, HttpStatusCode.Forbidden },
        { SystemRoles.Manager, "DELETE", CatalogClient.ProductsPath, HttpStatusCode.Forbidden },
        { SystemRoles.Admin, "DELETE", CatalogClient.ProductsPath, HttpStatusCode.NoContent },
        { SystemRoles.Staff, "POST-RESTORE", CatalogClient.ProductsPath, HttpStatusCode.Forbidden },
        { SystemRoles.Manager, "POST-RESTORE", CatalogClient.ProductsPath, HttpStatusCode.Forbidden },
        { SystemRoles.Admin, "POST-RESTORE", CatalogClient.ProductsPath, HttpStatusCode.OK },
        { SystemRoles.Staff, "DELETE", CatalogClient.CategoriesPath, HttpStatusCode.Forbidden },
        { SystemRoles.Manager, "DELETE", CatalogClient.CategoriesPath, HttpStatusCode.Forbidden },
        { SystemRoles.Admin, "DELETE", CatalogClient.CategoriesPath, HttpStatusCode.NoContent }
    };

    [Theory]
    [MemberData(nameof(RoleMatrix))]
    public async Task Each_operation_allows_exactly_the_intended_roles(
        string role,
        string method,
        string path,
        HttpStatusCode expected)
    {
        var token = (await _client.RegisterAndLoginAsync(role)).AccessToken;
        var admin = await _client.AdminTokenAsync();

        var isProducts = path == CatalogClient.ProductsPath;

        // Each case sets up only what its own operation needs. In particular the category used for a
        // category DELETE is left empty: deleting a category that still holds products is refused
        // with a 409 by design, and creating a product in every case would make an authorization
        // check look like a conflict.
        var category = await _client.CreateCategoryAsync(admin);
        var product = isProducts
            ? await _client.CreateProductAsync(admin, category.Id)
            : null;

        var response = method switch
        {
            "GET" => await _client.GetAsync(path, token, None),
            "POST" => await _client.PostAsAsync(path, isProducts ? ProductPayload(category.Id) : CategoryPayload(), token, None),
            "DELETE" => await _client.DeleteAsAsync(
                isProducts ? $"{path}/{product!.Id}" : $"{path}/{category.Id}",
                token,
                None),
            "POST-RESTORE" => await _client.PostAsAsync($"{path}/{product!.Id}/restore", new { }, token, None),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unhandled method.")
        };

        Assert.Equal(expected, response.StatusCode);

        // Every refusal uses the same ProblemDetails contract as the rest of the API.
        Assert.Equal(expected is HttpStatusCode.Forbidden, response.IsProblemDetails());
    }

    [Fact]
    public async Task A_forbidden_caller_cannot_delete_a_product_that_does_not_exist_either()
    {
        // Checking authorization before existence, so the response does not leak whether a given id
        // is real to someone who may not touch it.
        var staff = (await _client.RegisterAndLoginAsync(SystemRoles.Staff)).AccessToken;

        var response = await _client.DeleteAsAsync($"{CatalogClient.ProductsPath}/{Guid.NewGuid()}", staff, None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_is_unauthorized_on_reads_and_writes()
    {
        foreach (var path in new[] { CatalogClient.ProductsPath, CatalogClient.CategoriesPath })
        {
            var read = await _client.GetAsync(path, accessToken: null, None);
            var write = await _client.PostAsAsync(path, EmptyPayload, accessToken: null, None);

            Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
            Assert.True(read.IsProblemDetails());
            Assert.NotEmpty(read.Headers.WwwAuthenticate);
        }
    }

    [Fact]
    public async Task Staff_cannot_update_a_product()
    {
        var admin = await _client.AdminTokenAsync();
        var category = await _client.CreateCategoryAsync(admin);
        var product = await _client.CreateProductAsync(admin, category.Id);

        var staff = (await _client.RegisterAndLoginAsync(SystemRoles.Staff)).AccessToken;

        var response = await _client.PutAsAsync(
            $"{CatalogClient.ProductsPath}/{product.Id}",
            new
            {
                name = "Staff edit",
                description = (string?)null,
                categoryId = category.Id,
                unitOfMeasure = 0,
                reorderLevel = 0,
                costPrice = 1m,
                salePrice = 2m,
                isActive = true,
                rowVersion = product.RowVersion
            },
            staff,
            None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static object ProductPayload(Guid categoryId) =>
        new CreateProductRequest(
            $"AUTH-{Guid.NewGuid():N}"[..14], "Auth probe", null,
            categoryId, UnitOfMeasure.Piece, 0, 1m, 2m);

    private static object CategoryPayload() =>
        new CreateCategoryRequest($"AUTH-{Guid.NewGuid():N}"[..14], null, null);
}