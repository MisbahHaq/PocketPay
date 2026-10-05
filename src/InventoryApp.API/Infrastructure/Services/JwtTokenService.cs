using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryApp.Application.Abstractions;
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
            // Standard short names, mapped back to ClaimTypes by JwtBearerOptions.MapInboundClaims.
            // "sub" is what other JWT consumers expect; ICurrentUserService reads the mapped value.
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty)
        };

        // One claim per role. [Authorize(Roles=...)] and RequireRole() both work off these,
        // and a role change only takes effect on the next token, never mid-session.
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenResult(accessToken, new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }
}
