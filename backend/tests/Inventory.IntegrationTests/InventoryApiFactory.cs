using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Inventory.IntegrationTests;

/// <summary>
/// Boots the real API in-process against the shared test database.
/// </summary>
/// <remarks>
/// Everything the production pipeline does is left switched on - JWT validation, Identity,
/// authorization, ProblemDetails, the exception middleware. What is replaced is only the
/// ambient environment: the database it points at, the signing key, and the rate limits.
/// Limiting in particular is neutralised because these tests all arrive from the same
/// loopback address and would otherwise share a token bucket, turning an authentication
/// assertion into a 429.
/// </remarks>
public sealed class InventoryApiFactory(PostgresFixture fixture) : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-test-signing-key-not-a-real-secret-0123456789";

    private const string TestEnvironment = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestEnvironment);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
                ["Jwt:Issuer"] = "https://inventory.test",
                ["Jwt:Audience"] = "inventory.client",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
["Database:ApplyMigrationsOnStartup"] = "false",
                // No seeding: each test creates exactly the accounts it needs, so a test
                // never depends on a bootstrap admin existing. The keys match what
                // DatabaseSeeder actually reads (Seed:*, not Database:Admin:*).
                ["Seed:AdminEmail"] = string.Empty,
                ["Seed:AdminPassword"] = string.Empty,
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            });
        });

        builder.ConfigureServices(services =>
        {
            // A effectively unlimited limiter, applied last so it wins over the app's own
            // registration. Rate limiting has dedicated tests; it should never be the
            // reason an unrelated test fails.
            services.PostConfigure<RateLimiterOptions>(options =>
            {
                var unlimited = new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                };

                options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
                    _ => RateLimitPartition.GetFixedWindowLimiter("integration-tests", _ => unlimited));

                foreach (var policy in new[]
                         {
                             Inventory.Api.RateLimitPolicies.Login,
                             Inventory.Api.RateLimitPolicies.Refresh,
                         })
                {
                    options.AddPolicy(
                        policy,
                        _ => RateLimitPartition.GetFixedWindowLimiter("integration-tests", _ => unlimited));
                }
            });
        });
    }

    /// <summary>Empties every table, so each test starts from a known-empty database.</summary>
    public Task ResetAsync() => fixture.ResetAsync();

    /// <summary>Creates a confirmed user holding the given role.</summary>
    public async Task<SeededUser> CreateUserAsync(
        string role = SystemRoles.Staff,
        string password = TestPassword,
        string? email = null)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        email ??= $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

        var user = new ApplicationUser
        {
            Id = Domain.Common.SequentialGuid.New(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = $"{role} Test",
            IsActive = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var created = await userManager.CreateAsync(user, password);
        created.Succeeded.Should().BeTrue(
            $"seeding {role} failed: {string.Join("; ", created.Errors.Select(e => e.Description))}");

        (await userManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();

        return new SeededUser(user.Id, email);
    }

    public const string TestPassword = "Str0ng-Passw0rd!";

    /// <summary>Logs in, failing the test with the response body when it does not succeed.</summary>
    public async Task<TokenResponse> SignInAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"login failed: {(int)response.StatusCode} {body}");

        return JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("login returned an unparseable body.");
    }

    /// <summary>Creates a user and signs them in, returning both the account and its tokens.</summary>
    public async Task<(SeededUser User, TokenResponse Tokens)> SignInNewUserAsync(
        string role = SystemRoles.Staff)
    {
        var user = await CreateUserAsync(role);
        var tokens = await SignInAsync(CreateClient(), user.Email, TestPassword);

        return (user, tokens);
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

/// <summary>Helpers that need no state from the factory itself.</summary>
public static class AuthorizedClientExtensions
{
    public static HttpClient CreateAuthorizedClient(this InventoryApiFactory factory, TokenResponse tokens)
    {
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);

        return client;
    }
}

public sealed record SeededUser(Guid Id, string Email);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string TokenType,
    CurrentUserResponse User);

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, string[] Roles);

public sealed record MeResponse(Guid Id, string Email, string FullName, string[] Roles);