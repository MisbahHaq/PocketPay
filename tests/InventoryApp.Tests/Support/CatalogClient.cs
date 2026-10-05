using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Enums;
using InventoryApp.Domain.Security;

namespace InventoryApp.Tests.Support;

/// <summary>
/// Catalogue request builders. The scenarios then read as intent ("a product in the Tools
/// category") instead of as JSON plumbing.
/// </summary>
public static class CatalogClient
{
    public const string ProductsPath = "/api/products";
    public const string CategoriesPath = "/api/categories";

    /// <summary>Admin tokens are reused; other roles get a fresh throwaway account.</summary>
    public static async Task<string> TokenAsync(this HttpClient client, string role = SystemRoles.Admin) =>
        role == SystemRoles.Admin
            ? await client.AdminTokenAsync()
            : (await client.RegisterAndLoginAsync(role)).AccessToken;

    public static Task<HttpResponseMessage> PostAsAsync(
        this HttpClient client,
        string path,
        object payload,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Authorization = ToBearer(accessToken);
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<HttpResponseMessage> PutAsAsync(
        this HttpClient client,
        string path,
        object payload,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Authorization = ToBearer(accessToken);
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<HttpResponseMessage> DeleteAsAsync(
        this HttpClient client,
        string path,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Authorization = ToBearer(accessToken);
        return client.SendAsync(request, cancellationToken);
    }

    /// <summary>Matches Module 1's helper, so an absent token still sends an Authorization header.</summary>
    private static AuthenticationHeaderValue ToBearer(string? accessToken) =>
        string.IsNullOrEmpty(accessToken)
            ? new AuthenticationHeaderValue("Bearer")
            : new AuthenticationHeaderValue("Bearer", accessToken);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();

        return value ?? throw new InvalidOperationException("The response body was empty or not JSON.");
    }

    /// <summary>
    /// Asserts success and, when it is not, quotes the body. Without this a failing setup call
    /// reports "400 Bad Request" and hides the field errors that would explain it.
    /// </summary>
    public static async Task EnsureOkAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();

        throw new HttpRequestException(
            $"{(int)response.StatusCode} {response.StatusCode} from {response.RequestMessage?.Method} "
            + $"{response.RequestMessage?.RequestUri}: {body}");
    }

    /// <summary>Creates a category and returns it, so a test never repeats the POST.</summary>
    public static async Task<CategoryResponse> CreateCategoryAsync(
        this HttpClient client,
        string token,
        string? name = null,
        Guid? parentCategoryId = null)
    {
        var response = await client.PostAsAsync(
            CategoriesPath,
            new CreateCategoryRequest(name ?? $"CAT-{Guid.NewGuid():N}"[..20], null, parentCategoryId),
            token);

        await response.EnsureOkAsync();
        return await response.ReadAsync<CategoryResponse>();
    }

    public static async Task<ProductResponse> CreateProductAsync(
        this HttpClient client,
        string token,
        Guid categoryId,
        string? sku = null,
        string name = "Test Widget",
        decimal salePrice = 19.99m,
        decimal costPrice = 9.99m,
        bool isActive = true,
        UnitOfMeasure unit = UnitOfMeasure.Piece)
    {
        var response = await client.PostAsAsync(
            ProductsPath,
            new CreateProductRequest(
                sku ?? $"SKU-{Guid.NewGuid():N}"[..20],
                name,
                "Created by the integration tests.",
                categoryId,
                unit,
                10,
                costPrice,
                salePrice,
                isActive),
            token);

        await response.EnsureOkAsync();
        return await response.ReadAsync<ProductResponse>();
    }
}