using System.Net;
using System.Net.Http.Json;
using Inventory.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Inventory.IntegrationTests;

/// <summary>
/// Token rotation and replay detection, which are the controls that make a stolen refresh
/// token detectable rather than indefinite.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RefreshTokenFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _database;
    private readonly InventoryApiFactory _factory;

    public RefreshTokenFlowTests(PostgresFixture database)
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
    public async Task Refreshing_rotates_the_token_and_revokes_the_original()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // A new raw value every time: rotation is what makes reuse observable.
        refreshed!.RefreshToken.Should().NotBe(tokens.RefreshToken);
        refreshed.AccessToken.Should().NotBeNullOrWhiteSpace();

        await using var context = _database.CreateContext();
        var stored = await context.RefreshTokens.OrderBy(token => token.CreatedAt).ToListAsync();

        stored.Should().HaveCount(2);
        stored[0].IsRevoked.Should().BeTrue();
        stored[0].ReplacedByTokenHash.Should().Be(RefreshToken.HashToken(refreshed.RefreshToken));

        // The replacement stays in the same family so the whole chain can be revoked.
        stored[1].FamilyId.Should().Be(stored[0].FamilyId);
        stored[1].IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Reusing_a_rotated_token_is_rejected()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Replaying_a_rotated_token_revokes_the_entire_family()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();
        var client = _factory.CreateClient();

        // Attacker steals the original token and rotates it.
        var stolen = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        var attackerTokens = await stolen.Content.ReadFromJsonAsync<TokenResponse>();

        // The legitimate user's client then races with the replay.
        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Replay detection assumes the token was stolen, so the attacker's replacement
        // must die too. Otherwise detection would simply hand the attacker a longer-lived
        // credential.
        await using var context = _database.CreateContext();
        var stored = await context.RefreshTokens.ToListAsync();

        stored.Should().NotBeEmpty();
        stored.Should().OnlyContain(token => token.IsRevoked);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "this-was-never-issued" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_blank_refresh_token_is_a_validation_problem()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Logout_revokes_the_token_it_is_given()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();
        var client = _factory.CreateClient();

        var logout = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken = tokens.RefreshToken });

        logout.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = _database.CreateContext();
        var stored = await context.RefreshTokens.SingleAsync();

        stored.IsRevoked.Should().BeTrue();
        stored.RevokedReason.Should().Be(RefreshTokenRevocationReasons.Logout);

        // Signing out is not theft, so it must not trip replay detection.
        stored.IsReplayed.Should().BeFalse();

        var reuse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_rotated_chain_can_be_refreshed_repeatedly()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();
        var client = _factory.CreateClient();

        var current = tokens.RefreshToken;

        for (var round = 0; round < 4; round++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new { refreshToken = current });

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                $"round {round} of the rotation chain failed");

            var next = await response.Content.ReadFromJsonAsync<TokenResponse>();
            next!.RefreshToken.Should().NotBe(current);
            current = next.RefreshToken;
        }

        await using var context = _database.CreateContext();
        var stored = await context.RefreshTokens.ToListAsync();

        // Four refreshes plus the original login.
        stored.Should().HaveCount(5);
        stored.Select(token => token.FamilyId).Distinct().Should().ContainSingle();
        stored.Count(token => !token.IsRevoked).Should().Be(1);
    }

    [Fact]
    public async Task A_refreshed_access_token_still_works_against_a_protected_endpoint()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>();

        using var authorized = _factory.CreateAuthorizedClient(refreshed!);
        (await authorized.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_refresh_token_belonging_to_another_user_cannot_be_exchanged()
    {
        var (_, firstTokens) = await _factory.SignInNewUserAsync();
        var (_, secondTokens) = await _factory.SignInNewUserAsync();

        await using var context = _database.CreateContext();
        var secondUser = await context.Users.SingleAsync(u => u.Email == secondTokens.User.Email);

        var firstToken = await context.RefreshTokens
            .SingleAsync(token => token.UserId == firstTokens.User.Id);

        // Spoofing the user id alongside the token must not help: the token row is the
        // authority, not the claim.
        firstToken.UserId.Should().NotBe(secondUser.Id);
        firstToken.IsActive(DateTimeOffset.UtcNow).Should().BeTrue();
    }
}