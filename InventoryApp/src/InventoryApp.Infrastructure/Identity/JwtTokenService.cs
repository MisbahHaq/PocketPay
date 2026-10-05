using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryApp.Application.Auth;
using InventoryApp.Application.Common.Interfaces;
using InventoryApp.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryApp.Infrastructure.Identity;

/// <summary>
/// Mints HS256-signed access tokens. Stateless: nothing is written to the
/// database on login, which is the main reason access tokens are short-lived
/// (15 minutes) rather than long-lived.
/// </summary>
public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken CreateAccessToken(
        ApplicationUser user,
        IReadOnlyCollection<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>(4 + roles.Count)
        {
            new(AuthClaimTypes.UserId, user.Id),
            new(AuthClaimTypes.Email, user.Email ?? string.Empty),
            new(AuthClaimTypes.TokenId, Guid.NewGuid().ToString("N")),
        };

        // One "role" claim per role (rather than a single space-delimited claim)
        // so ASP.NET Core's authorization reads them individually.
        claims.AddRange(roles.Select(role => new Claim(AuthClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            roles);
    }
}
