using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Inventory.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.IntegrationTests;

/// <summary>
/// The Phase 1 HTTP contract, exercised end to end against a real PostgreSQL database.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthenticationEndpointTests : IAsyncLifetime
{
    private const string Password = InventoryApiFactory.TestPassword;

    private readonly PostgresFixture _database;
    private readonly InventoryApiFactory _factory;

    public AuthenticationEndpointTests(PostgresFixture database)
    {
        _database = database;
        _factory = new InventoryApiFactory(database);
    }

    public async Task InitializeAsync()
    {
        _factory.CreateClient();
        await _factory.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_returns_a_token_pair_and_the_user_profile()
    {
        var user = await _factory.CreateUserAsync(SystemRoles.Manager);

        var tokens = await _factory.SignInAsync(_factory.CreateClient(), user.Email, Password);

        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.TokenType.Should().Be("Bearer");
        tokens.User.Email.Should().Be(user.Email);
        tokens.User.Roles.Should().BeEquivalentTo([SystemRoles.Manager]);
        tokens.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_never_echoes_the_password_or_its_hash()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = Password });

        var raw = await response.Content.ReadAsStringAsync();
        var lower = raw.ToLowerInvariant();

        lower.Should().NotContain("passwordhash");
        lower.Should().NotContain("password_hash");
        raw.Should().NotContain(Password, "the response must not echo the submitted secret");
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_account_are_indistinguishable()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = "Wr0ng-Passw0rd!" });

        var unknownAccount = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "nobody@example.com", password = Password });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownAccount.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Byte-identical bodies: any difference in status, wording or timing would turn
        // this endpoint into a user enumeration oracle.
        (await wrongPassword.Content.ReadAsStringAsync())
            .Should().Be(await unknownAccount.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_malformed_login_is_a_validation_problem_not_a_crash()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "not-an-email", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<ProblemShape>();
        problem!.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task The_access_token_authenticates_a_subsequent_request()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        me!.Email.Should().Be(tokens.User.Email);
        me.Roles.Should().BeEquivalentTo(tokens.User.Roles);
    }

    [Fact]
    public async Task An_anonymous_request_is_rejected()
    {
        (await _factory.CreateClient().GetAsync("/api/v1/auth/me"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_garbage_token_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.token");

        (await client.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_signed_with_an_attacker_key_is_rejected()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();

        // Same claims, same issuer and audience, re-signed with a key the server does not
        // trust. Validating only issuer/audience would accept this.
        var forged = ResignWithOtherKey(tokens.AccessToken);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        (await client.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_inactive_account_cannot_log_in()
    {
        var user = await _factory.CreateUserAsync();

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
            stored.IsActive = false;
            await context.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_successful_login_records_last_login_at()
    {
        var user = await _factory.CreateUserAsync();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        await _factory.SignInAsync(_factory.CreateClient(), user.Email, Password);

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);

        stored.LastLoginAt.Should().NotBeNull();
        stored.LastLoginAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task Repeated_failures_eventually_lock_the_account_out()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = user.Email, password = "Wr0ng-Passw0rd!" });
        }

        // Correct password, but the account is now locked. Without lockout a six guess
        // per minute budget is all an attacker gets, and lockout makes guessing cost the
        // defender real time.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        (stored.LockoutEnd > DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task Staff_cannot_create_users()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Staff);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"escalated-{Guid.NewGuid():N}@example.com",
                password = Password,
                confirmPassword = Password,
                fullName = "Escalated User",
                role = SystemRoles.Admin,
            });

        // Staff must not be able to mint an Admin, least of all for another account.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_cannot_create_users()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Manager);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"manager-{Guid.NewGuid():N}@example.com",
                password = Password,
                confirmPassword = Password,
                fullName = "Manager Attempt",
            });

        // User administration is Admin only, not "everyone above Staff".
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_create_a_user_and_assign_a_role()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);

        using var authorized = _factory.CreateAuthorizedClient(tokens);

        var email = $"created-{Guid.NewGuid():N}@example.com";
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email,
                password = Password,
                confirmPassword = Password,
                fullName = "New Starter",
                role = SystemRoles.Staff,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        created!.Email.Should().Be(email);
        created.Roles.Should().BeEquivalentTo([SystemRoles.Staff]);

        // The account must be immediately usable, not merely present in a table.
        var newTokens = await _factory.SignInAsync(_factory.CreateClient(), email, Password);
        newTokens.User.Roles.Should().BeEquivalentTo([SystemRoles.Staff]);
    }

    [Fact]
    public async Task A_created_account_defaults_to_staff_when_no_role_is_given()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"default-{Guid.NewGuid():N}@example.com",
                password = Password,
                confirmPassword = Password,
                fullName = "No Role Given",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!
            .Roles.Should().BeEquivalentTo([SystemRoles.Staff]);
    }

    [Fact]
    public async Task A_duplicate_account_is_a_conflict_not_a_server_error()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);
        var existing = await _factory.CreateUserAsync(SystemRoles.Staff);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = existing.Email,
                password = Password,
                confirmPassword = Password,
                fullName = "Clash",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_weak_password_is_a_validation_problem()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"weak-{Guid.NewGuid():N}@example.com",
                password = "weak",
                confirmPassword = "weak",
                fullName = "Weak Password",
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task An_unknown_role_is_rejected()
    {
        // Regression guard: the validator once used IsInEnum() against a string property,
        // which silently rejected every role including all three real ones.
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"superuser-{Guid.NewGuid():N}@example.com",
                password = Password,
                confirmPassword = Password,
                fullName = "Pretend Admin",
                role = "Superuser",
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_role_is_accepted_regardless_of_casing()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"lower-{Guid.NewGuid():N}@example.com",
                password = Password,
                confirmPassword = Password,
                fullName = "Lowercase Role",
                role = "staff",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!
            .Roles.Should().BeEquivalentTo([SystemRoles.Staff]);
    }

    [Fact]
    public async Task A_blank_password_is_rejected_with_one_clear_message()
    {
        // Regression guard: NotEmpty used to sit inside a .When(...) guarded chain, which
        // silenced it along with the complexity rules, so a blank password produced no
        // error at all.
        var (_, tokens) = await _factory.SignInNewUserAsync(SystemRoles.Admin);

        using var authorized = _factory.CreateAuthorizedClient(tokens);
        var response = await authorized.PostAsJsonAsync(
            "/api/v1/auth/users",
            new
            {
                email = $"blank-{Guid.NewGuid():N}@example.com",
                password = "",
                confirmPassword = "",
                fullName = "Blank Password",
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<ProblemShape>();
        problem!.Errors.Should().ContainKey("password");
        problem.Errors["password"].Length.Should().Be(1);
    }

    /// <summary>Re-signs a token with a key the server does not trust, keeping the claims.</summary>
    private static string ResignWithOtherKey(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var read = handler.ReadJwtToken(token);

        // The audience is restated explicitly: the point of this test is a valid issuer and
        // audience paired with the wrong signing key, so only the key differs.
        var forged = new JwtSecurityToken(
            read.Issuer,
            "inventory.client",
            read.Claims.Where(claim => claim.Type is not ("exp" or "nbf")),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("an-attacker-signing-key-that-is-long-enough-here")),
                SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(forged);
    }

    private sealed record ProblemShape(IDictionary<string, string[]> Errors);
}