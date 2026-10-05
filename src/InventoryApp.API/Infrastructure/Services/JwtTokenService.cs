using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Common.Options;
using InventoryApp.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryApp.Infrastructure.Services;

/// <summary>Issues HMAC-SHA256 signed JWT access tokens. Lives in Infrastructure because signing is a technical concern.</summary>
internal sealed class JwtTokenService(IOptions<JwtTokenOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtTokenOptions _options = options.Value;

    public AccessTokenResult CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AuthClaimTypes.UserId, user.Id),
            new(AuthClaimTypes.Email, user.Email ?? string.Empty),
            new(AuthClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(AuthClaimTypes.TokenId, Guid.NewGuid().ToString())
        };

        // One claim per role. RequireRole and IsInRole both read these, and a role change only
        // takes effect on the next issued token, never part-way through a session.
        claims.AddRange(roles.Select(role => new Claim(AuthClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var accessToken = new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);

        return new AccessTokenResult(accessToken, new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }
}