using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryApp.Application.Common;
using InventoryApp.Application.Common.Options;
using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Security;
using InventoryApp.Infrastructure.Services;
using InventoryApp.Tests.Support;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryApp.Tests.Unit;

/// <summary>
/// Unit tests for access token creation. Every assertion about trust is made against the same
/// validation parameters the API uses, so a mismatch between issuer and validator fails here.
/// </summary>
public class JwtTokenServiceTests
{
    private const string Issuer = "InventoryApp.Api";
    private const string Audience = "InventoryApp.Clients";
    private const string SigningKey = "unit-test-signing-key-of-at-least-32-characters";

    // Anchored to the real clock: lifetime validation inside the token handler uses the system
    // clock, so a token minted in the distant past would be rejected as expired.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateAccessToken_EmbedsUserIdEmailAndEveryRole()
    {
        var result = CreateService().CreateAccessToken(
            NewUser(),
            [SystemRoles.Staff, SystemRoles.Manager]);

        var token = Read(result.Token);

        Assert.Equal("user-42", ClaimValue(token, AuthClaimTypes.UserId));
        Assert.Equal("staff@test.local", ClaimValue(token, AuthClaimTypes.Email));
        Assert.NotEmpty(ClaimValue(token, AuthClaimTypes.TokenId));
        Assert.Equal(
            [SystemRoles.Manager, SystemRoles.Staff],
            RolesIn(token).OrderBy(role => role, StringComparer.Ordinal));
    }

    [Fact]
    public void CreateAccessToken_EmitsNoRoleClaimWhenTheUserHasNoRoles()
    {
        var token = Read(CreateService().CreateAccessToken(NewUser(), []).Token);

        Assert.Empty(RolesIn(token));
    }

    [Fact]
    public void CreateAccessToken_UsesConfiguredIssuerAudienceAndLifetime()
    {
        var result = CreateService(accessTokenMinutes: 15).CreateAccessToken(NewUser(), [SystemRoles.Staff]);
        var token = Read(result.Token);

        Assert.Equal(Issuer, token.Issuer);
        Assert.Equal([Audience], token.Audiences);
        Assert.Equal(15 * 60, (int)(token.ValidTo - token.ValidFrom).TotalSeconds);
        Assert.Equal(new DateTimeOffset(Now.UtcDateTime.AddMinutes(15)), result.ExpiresAt);
    }

    [Fact]
    public void CreateAccessToken_HonoursAShorterConfiguredLifetime()
    {
        var result = CreateService(accessTokenMinutes: 5).CreateAccessToken(NewUser(), [SystemRoles.Staff]);

        Assert.Equal(new DateTimeOffset(Now.UtcDateTime.AddMinutes(5)), result.ExpiresAt);
    }

    [Fact]
    public void CreateAccessToken_IsAcceptedByTheProductionValidationParameters()
    {
        var result = CreateService().CreateAccessToken(NewUser(), [SystemRoles.Admin]);

        var principal = Validate(result.Token, SigningKey);

        // MapInboundClaims is off in production too, so the claims are checked by their real names.
        Assert.Equal("user-42", principal.FindFirst(AuthClaimTypes.UserId)?.Value);
        Assert.Equal("staff@test.local", principal.FindFirst(AuthClaimTypes.Email)?.Value);
        Assert.True(principal.IsInRole(SystemRoles.Admin));
        Assert.True(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public void CreateAccessToken_IsRejectedWhenSignedWithADifferentKey()
    {
        var token = CreateService().CreateAccessToken(NewUser(), [SystemRoles.Admin]).Token;

        Assert.ThrowsAny<SecurityTokenException>(
            () => Validate(token, "an-entirely-different-signing-key-of-32chars"));
    }

    [Fact]
    public void CreateAccessToken_IsRejectedAfterItExpires()
    {
        // Issued two hours ago, so it is well outside the 30 second clock skew.
        var token = CreateService(now: Now.AddHours(-2))
            .CreateAccessToken(NewUser(), [SystemRoles.Staff])
            .Token;

        Assert.ThrowsAny<SecurityTokenException>(() => Validate(token, SigningKey));
    }

    [Fact]
    public void CreateAccessToken_IsRejectedForAnotherIssuer()
    {
        var token = CreateService().CreateAccessToken(NewUser(), [SystemRoles.Staff]).Token;
        var parameters = ValidationParameters(SigningKey);
        parameters.ValidIssuer = "Some.Other.Issuer";

        Assert.ThrowsAny<SecurityTokenException>(
            () => Validate(token, parameters));
    }

    [Fact]
    public void CreateAccessToken_IssuesADistinctTokenIdEachTime()
    {
        var service = CreateService();

        var first = ClaimValue(Read(service.CreateAccessToken(NewUser(), [SystemRoles.Staff]).Token), AuthClaimTypes.TokenId);
        var second = ClaimValue(Read(service.CreateAccessToken(NewUser(), [SystemRoles.Staff]).Token), AuthClaimTypes.TokenId);

        Assert.NotEqual(first, second);
    }

    private static ApplicationUser NewUser() => new()
    {
        Id = "user-42",
        UserName = "staff@test.local",
        Email = "staff@test.local",
        FullName = "Sam Staff",
        IsActive = true
    };

    private static JwtTokenService CreateService(int accessTokenMinutes = 15, DateTimeOffset? now = null) =>
        new(
            Options.Create(new JwtTokenOptions
            {
                Issuer = Issuer,
                Audience = Audience,
                SigningKey = SigningKey,
                AccessTokenMinutes = accessTokenMinutes
            }),
            new FixedTimeProvider(now ?? Now));

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string ClaimValue(JwtSecurityToken token, string type) =>
        token.Claims.Single(claim => claim.Type == type).Value;

    private static IEnumerable<string> RolesIn(JwtSecurityToken token) =>
        token.Claims.Where(claim => claim.Type == AuthClaimTypes.Role).Select(claim => claim.Value);

    private static TokenValidationParameters ValidationParameters(string signingKey) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        RoleClaimType = AuthClaimTypes.Role,
        NameClaimType = AuthClaimTypes.Name,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private static ClaimsPrincipal Validate(string token, string signingKey) =>
        Validate(token, ValidationParameters(signingKey));

    private static ClaimsPrincipal Validate(string token, TokenValidationParameters parameters)
    {
        // MapInboundClaims is off in production, so the handler must not remap claims either.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, parameters, out _);
    }
}