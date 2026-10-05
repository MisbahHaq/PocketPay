using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Security;

namespace InventoryApp.Tests.Support;

/// <summary>Shared HTTP helpers so each test reads as scenario rather than as plumbing.</summary>
public static class ApiClientExtensions
{
    public static async Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client,
        string path,
        object payload,
        CancellationToken cancellationToken = default) =>
        await client.PostAsJsonAsync(path, payload, cancellationToken);

    public static async Task<TokenResponse> ReadTokensAsync(this HttpResponseMessage response)
    {
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        return tokens ?? throw new InvalidOperationException("The response did not contain a token pair.");
    }

    public static Task<HttpResponseMessage> PostAsync(
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

    public static Task<HttpResponseMessage> GetAsync(
        this HttpClient client,
        string path,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = ToBearer(accessToken);
        return client.SendAsync(request, cancellationToken);
    }

    private static AuthenticationHeaderValue ToBearer(string? accessToken) =>
        string.IsNullOrEmpty(accessToken)
            ? new AuthenticationHeaderValue("Bearer")
            : new AuthenticationHeaderValue("Bearer", accessToken);

    public static RegisterUserRequest Registration(string role, string? email = null) =>
        new(
            Email: email ?? $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local",
            FullName: $"{role} Test User",
            Password: "Str0ng!Password1",
            Role: role);

    public static async Task<TokenResponse> RegisterAndLoginAsync(
        this HttpClient adminClient,
        string role,
        string password = "Str0ng!Password1")
    {
        var registration = Registration(role);
        var created = await adminClient.PostAsync("/api/auth/register", registration, await adminClient.AdminTokenAsync());
        created.EnsureSuccessStatusCode();

        var login = await adminClient.PostJsonAsync("/api/auth/login", new LoginRequest(registration.Email, password));
        login.EnsureSuccessStatusCode();

        return await login.ReadTokensAsync();
    }

    public static async Task<string> AdminTokenAsync(this HttpClient client)
    {
        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, InventoryApiFactory.AdminPassword));

        response.EnsureSuccessStatusCode();
        return (await response.ReadTokensAsync()).AccessToken;
    }

    public static bool IsProblemDetails(this HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "application/problem+json";

    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}