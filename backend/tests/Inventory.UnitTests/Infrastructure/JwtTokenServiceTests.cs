using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Inventory.Application.Abstractions;
using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Inventory.UnitTests.Infrastructure;

/// <summary>Fixed clock, so token expiry is asserted rather than slept for.</summary>
internal sealed class FixedDateTimeProvider(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = now;
}

public sealed class JwtTokenServiceTests
{
    private const string SigningKey = "test-signing-key-that-is-definitely-long-enough-1234";

    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static JwtTokenService CreateSut(int accessTokenMinutes = 15) =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = "https://inventory.test",
                Audience = "inventory.client",
                SigningKey = SigningKey,
                AccessTokenMinutes = accessTokenMinutes,
            }),
            new FixedDateTimeProvider(Now));

    private static ApplicationUser CreateUser(params string[] roles) => new()
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        UserName = "alex",
        Email = "alex@example.com",
        FullName = "Alex Okonkwo",
    };

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static ClaimsPrincipal Validate(
        string token,
        string signingKey,
        bool checkLifetime = true)
    {
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "https://inventory.test",
                ValidateAudience = true,
                ValidAudience = "inventory.client",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = checkLifetime,
            },
            out _);

        return principal;
    }

    [Fact]
    public void Issues_a_signed_token()
    {
        var issued = CreateSut().CreateAccessToken(CreateUser(), []);

        issued.Value.Should().NotBeNullOrWhiteSpace();

        // Three JWS segments, and an explicit algorithm rather than "none". Either check
        // failing means the bearer handler would reject it at the first real login.
        issued.Value.Split('.').Should().HaveCount(3);
        Read(issued.Value).Header.Alg.Should().Be("HS256");
    }

    [Fact]
    public void Is_accepted_by_the_bearer_handler_with_the_configured_key()
    {
        // Round-trip through the same validation the API performs, which is the only way
        // to know the token will actually authenticate.
        var issued = CreateSut().CreateAccessToken(CreateUser(), ["Manager"]);

        // Lifetime checking is off because the service is on an injected clock
        // (March) while the handler reads the real system clock (October); the exact
        // window is asserted separately against the injected clock.
        var principal = Validate(issued.Value, SigningKey, checkLifetime: false);

        // The handler maps inbound claims by default, so "sub" surfaces as
        // ClaimTypes.NameIdentifier. That mapping is why authorization code can rely
        // on the well-known type, so the test asserts the mapped form.
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(CreateUser().Id.ToString());
        principal.IsInRole("Manager").Should().BeTrue();
    }

    [Fact]
    public void Uses_the_configured_issuer_and_audience()
    {
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), []).Value);

        token.Issuer.Should().Be("https://inventory.test");
        token.Audiences.Should().ContainSingle().Which.Should().Be("inventory.client");
    }

    [Fact]
    public void Expires_after_the_configured_lifetime()
    {
        var issued = CreateSut(accessTokenMinutes: 20).CreateAccessToken(CreateUser(), []);

        issued.ExpiresAt.Should().Be(Now.AddMinutes(20));
    }

    [Fact]
    public void Carries_the_user_id_so_the_current_user_can_be_resolved()
    {
        var user = CreateUser();

        var token = Read(CreateSut().CreateAccessToken(user, []).Value);

        token.Claims.Should().Contain(claim =>
            claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == user.Id.ToString());
    }

    [Fact]
    public void Carries_a_unique_token_id()
    {
        // The "jti" is what makes an access token individually denylable in the future.
        var first = Read(CreateSut().CreateAccessToken(CreateUser(), []).Value);
        var second = Read(CreateSut().CreateAccessToken(CreateUser(), []).Value);

        var firstId = first.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var secondId = second.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        firstId.Should().NotBe(secondId);
    }

    [Fact]
    public void Emits_only_the_roles_supplied()
    {
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), ["Manager"]).Value);

        token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(["Manager"]);
    }

    [Fact]
    public void Does_not_invent_a_role_when_none_are_supplied()
    {
        // A default role claim would let a user with no roles pass every Staff-gated
        // endpoint until the token expired.
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), []).Value);

        token.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public void Carries_multiple_roles()
    {
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), ["Admin", "Manager"]).Value);

        token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(["Admin", "Manager"]);
    }

    [Fact]
    public void Collapses_duplicate_roles()
    {
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), ["Manager", "manager", "MANAGER"]).Value);

        token.Claims.Count(c => c.Type == ClaimTypes.Role).Should().Be(1);
    }

    [Fact]
    public void Ignores_blank_role_entries()
    {
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), ["Manager", "", "   "]).Value);

        token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(["Manager"]);
    }

    [Fact]
    public void Carries_the_display_name_and_email()
    {
        var token = Read(CreateSut().CreateAccessToken(CreateUser(), []).Value);

        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "Alex Okonkwo");
        token.Claims.Should().Contain(c => c.Type == "email" && c.Value == "alex@example.com");
    }

    [Fact]
    public void Bounds_the_validity_window_to_the_configured_lifetime()
    {
        var token = Read(CreateSut(accessTokenMinutes: 15).CreateAccessToken(CreateUser(), []).Value);

        // A short, exact window is what limits the damage from a stolen access token.
        // Asserted against the injected clock rather than by sleeping. ValidFrom/ValidTo
        // are DateTime (UTC), not DateTimeOffset.
        token.ValidFrom.Should().Be(Now.UtcDateTime);
        token.ValidTo.Should().Be(Now.AddMinutes(15).UtcDateTime);

        (token.ValidTo - token.ValidFrom).Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void Rejects_a_token_signed_with_a_different_key()
    {
        var token = CreateSut().CreateAccessToken(CreateUser(), []).Value;

        var act = () => Validate(token, "a-completely-different-signing-key-also-long-enough");

        act.Should().Throw<Microsoft.IdentityModel.Tokens.SecurityTokenSignatureKeyNotFoundException>();
    }

    [Fact]
    public void Rejects_a_token_issued_for_a_different_audience()
    {
        var token = CreateSut().CreateAccessToken(CreateUser(), []).Value;

        var act = () => new JwtSecurityTokenHandler().ValidateToken(
            token,
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "https://inventory.test",
                ValidateAudience = true,
                ValidAudience = "some.other.client",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(SigningKey)),
                ValidateLifetime = false,
            },
            out _);

        act.Should().Throw<Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public void Rejects_a_null_user()
    {
        var act = () => CreateSut().CreateAccessToken(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }
}