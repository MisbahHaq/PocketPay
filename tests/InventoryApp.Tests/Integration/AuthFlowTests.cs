using System.Net;
using System.Net.Http.Json;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Security;
using InventoryApp.Tests.Support;

namespace InventoryApp.Tests.Integration;

/// <summary>End-to-end coverage of the register, login, refresh and logout flows.</summary>
public class AuthFlowTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WithTheSeededAdmin_ReturnsBothTokens()
    {
        var response = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, InventoryApiFactory.AdminPassword),
            None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.ReadTokensAsync();

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);

        // 15 minute access token, 7 day refresh token.
        Assert.Equal(15, (tokens.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalMinutes, precision: 0);
        Assert.Equal(7, (tokens.RefreshTokenExpiresAt - DateTimeOffset.UtcNow).TotalDays, precision: 0);
    }

    [Fact]
    public async Task Login_WithAWrongPasswordAndWithAnUnknownEmail_FailIdentically()
    {
        var wrongPassword = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, "Wr0ng!Password"),
            None);

        var unknownEmail = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest("nobody@test.local", "Wr0ng!Password"),
            None);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        // Byte-identical bodies: the endpoint cannot be used to discover registered addresses.
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownEmail.Content.ReadAsStringAsync());

        var problem = await unknownEmail.ReadProblemAsync();
        Assert.Equal("Unauthorized", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_WithAMalformedEmail_ReturnsAValidationProblem()
    {
        var response = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest("not-an-email", "Str0ng!Password1"),
            None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemDetails());

        var problem = await response.ReadProblemAsync();
        Assert.Equal("Validation failed", problem.GetProperty("title").GetString());
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterRepeatedFailures()
    {
        var adminToken = await _client.AdminTokenAsync();
        var registration = ApiClientExtensions.Registration(SystemRoles.Staff);
        (await _client.PostAsync("/api/auth/register", registration, adminToken, None)).EnsureSuccessStatusCode();

        // Five failures reach the configured threshold without the response changing.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await _client.PostJsonAsync(
                "/api/auth/login",
                new LoginRequest(registration.Email, "Wr0ng!Password"),
                None);

            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            Assert.Equal("Invalid email or password.", (await failed.ReadProblemAsync()).GetProperty("detail").GetString());
        }

        // The correct password no longer helps while the lockout stands.
        var locked = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(registration.Email, registration.Password),
            None);

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("locked", (await locked.ReadProblemAsync()).GetProperty("detail").GetString() ?? string.Empty);
    }

    [Fact]
    public async Task Register_WithAnAdminToken_CreatesTheUserWithTheRequestedRole()
    {
        var adminToken = await _client.AdminTokenAsync();
        var registration = ApiClientExtensions.Registration(SystemRoles.Manager);

        var response = await _client.PostAsync("/api/auth/register", registration, adminToken, None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(created);
        Assert.Equal(registration.Email.ToUpperInvariant(), created!.Email);
        Assert.Equal(SystemRoles.Manager, Assert.Single(created.Roles));
        Assert.True(created.IsActive);

        // The user can immediately sign in with the provisioned password.
        var login = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(registration.Email, registration.Password), None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutAToken_IsRejected()
    {
        var response = await _client.PostJsonAsync(
            "/api/auth/register",
            ApiClientExtensions.Registration(SystemRoles.Staff),
            None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemDetails());
    }

    [Fact]
    public async Task Register_WithANonAdminToken_IsForbidden()
    {
        var adminToken = await _client.AdminTokenAsync();
        var staffTokens = await _client.RegisterAndLoginAsync(SystemRoles.Staff);

        var response = await _client.PostAsync(
            "/api/auth/register",
            ApiClientExtensions.Registration(SystemRoles.Admin),
            staffTokens.AccessToken,
            None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemDetails());

        // Sanity check that the admin token used to set this up really is an admin.
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsync("/api/auth/register", ApiClientExtensions.Registration(SystemRoles.Staff), adminToken, None)).StatusCode);
    }

    [Fact]
    public async Task Register_WithAWeakPassword_ReturnsAValidationProblem()
    {
        var adminToken = await _client.AdminTokenAsync();

        var response = await _client.PostAsync(
            "/api/auth/register",
            new RegisterUserRequest("weak@test.local", "Weak Password", "password", SystemRoles.Staff),
            adminToken,
            None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await response.ReadProblemAsync()).GetProperty("errors");
        var messages = errors.EnumerateObject().SelectMany(property => property.Value.EnumerateArray()).Select(value => value.GetString());
        Assert.Contains(messages, message => message!.Contains("uppercase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_WithAnExistingEmail_IsRejected()
    {
        var adminToken = await _client.AdminTokenAsync();
        var registration = ApiClientExtensions.Registration(SystemRoles.Staff);

        (await _client.PostAsync("/api/auth/register", registration, adminToken, None)).EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsync("/api/auth/register", registration, adminToken, None);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsTheIdentityAndRolesFromTheAccessToken()
    {
        var adminToken = await _client.AdminTokenAsync();

        var response = await _client.GetAsync("/api/auth/me", adminToken, None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(me);
        Assert.Equal(InventoryApiFactory.AdminEmail.ToUpperInvariant(), me!.Email);
        Assert.Equal(SystemRoles.Admin, Assert.Single(me.Roles));
        Assert.False(string.IsNullOrWhiteSpace(me.Id));
    }

    [Fact]
    public async Task Refresh_RotatesBothTokens()
    {
        var login = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, InventoryApiFactory.AdminPassword),
            None);

        var original = await login.ReadTokensAsync();

        var response = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(original.RefreshToken), None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rotated = await response.ReadTokensAsync();
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(rotated.AccessToken));

        // The new access token works on a protected endpoint.
        var me = await _client.GetAsync("/api/auth/me", rotated.AccessToken, None);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnAlreadyRotatedToken_RevokesTheWholeFamily()
    {
        var login = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, InventoryApiFactory.AdminPassword),
            None);

        var first = await login.ReadTokensAsync();
        var second = await (await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(first.RefreshToken), None)).ReadTokensAsync();

        // Replaying the stolen copy trips reuse detection.
        var replay = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(first.RefreshToken), None);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // Which also kills the legitimate client's token: both are assumed compromised.
        var legitimate = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(second.RefreshToken), None);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAGarbageToken_IsUnauthorized()
    {
        var response = await _client.PostJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest("this-was-never-issued"),
            None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var login = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, InventoryApiFactory.AdminPassword),
            None);

        var tokens = await login.ReadTokensAsync();

        var logout = await _client.PostJsonAsync("/api/auth/logout", new LogoutRequest(tokens.RefreshToken), None);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken), None);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_IsIdempotent()
    {
        var login = await _client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(InventoryApiFactory.AdminEmail, InventoryApiFactory.AdminPassword),
            None);

        var tokens = await login.ReadTokensAsync();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.PostJsonAsync("/api/auth/logout", new LogoutRequest(tokens.RefreshToken), None)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.PostJsonAsync("/api/auth/logout", new LogoutRequest(tokens.RefreshToken), None)).StatusCode);

        // And an unknown token is still a successful, silent logout.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.PostJsonAsync("/api/auth/logout", new LogoutRequest("never-issued"), None)).StatusCode);
    }
}