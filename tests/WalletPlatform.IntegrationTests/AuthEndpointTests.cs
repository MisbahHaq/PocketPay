using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class AuthEndpointTests(ApiEnvironment environment)
{
    private const string Password = "Str0ng!Passw0rd";

    private static string UniqueEmail() =>
        $"user+{Guid.NewGuid():N}@example.com";

    private sealed record AuthResponse(string AccessToken, string RefreshToken);

    private sealed record WalletResponse(string Id, string WalletNumber, string Currency);

    private sealed record MeResponse(Guid Id, string Email, string Role, WalletResponse? Wallet);

    [Fact]
    public async Task Register_login_and_read_profile_end_to_end()
    {
        var client = environment.Factory.CreateClient();
        var email = UniqueEmail();

        var registerResponse = await client.PostJsonAsync(
            "/api/auth/register",
            new { email, password = Password });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResponse = await client.PostJsonAsync(
            "/api/auth/login",
            new { email, password = Password });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.ReadAsAsync<AuthResponse>();

        var meResponse = await client.GetAuthorizedAsync("/api/users/me", auth.AccessToken);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.ReadAsAsync<MeResponse>();
        me.Email.Should().Be(email);
        me.Role.Should().Be("Customer");
        me.Wallet.Should().NotBeNull();
        me.Wallet!.WalletNumber.Should().MatchRegex("^[0-9]{12}$");
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_conflict()
    {
        var client = environment.Factory.CreateClient();
        var email = UniqueEmail();

        await client.PostJsonAsync("/api/auth/register", new { email, password = Password });

        var duplicate = await client.PostJsonAsync("/api/auth/register", new { email, password = Password });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_validation_problem()
    {
        var client = environment.Factory.CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), password = "weak" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!.Should().ContainKey("errors");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_generic_unauthorized()
    {
        var client = environment.Factory.CreateClient();
        var email = UniqueEmail();

        await client.PostJsonAsync("/api/auth/register", new { email, password = Password });

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new { email, password = "Wrong!Passw0rd" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Profile_without_token_returns_unauthorized()
    {
        var client = environment.Factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_endpoint_with_customer_token_returns_forbidden()
    {
        var client = environment.Factory.CreateClient();
        var email = UniqueEmail();

        await client.PostJsonAsync("/api/auth/register", new { email, password = Password });
        var login = await client.PostJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.ReadAsAsync<AuthResponse>();

        var response = await client.GetAuthorizedAsync("/api/admin/users", auth.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refreshing_with_a_reused_token_revokes_the_whole_family()
    {
        var client = environment.Factory.CreateClient();
        var email = UniqueEmail();

        await client.PostJsonAsync("/api/auth/register", new { email, password = Password });
        var login = await client.PostJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.ReadAsAsync<AuthResponse>();

        var rotated = await client.PostJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedAuth = await rotated.ReadAsAsync<AuthResponse>();

        var reuse = await client.PostJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var afterReuse = await client.PostJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = rotatedAuth.RefreshToken });
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_rolls_back_user_when_wallet_creation_fails()
    {
        var factory = environment.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<
                    WalletPlatform.Application.Abstractions.Security.IWalletNumberGenerator,
                    ThrowingWalletNumberGenerator>()));

        var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var login = await client.PostJsonAsync(
            "/api/auth/login",
            new { email, password = Password });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed class ThrowingWalletNumberGenerator
        : WalletPlatform.Application.Abstractions.Security.IWalletNumberGenerator
    {
        public string Generate() =>
            throw new InvalidOperationException("Simulated wallet number generation failure.");
    }
}
