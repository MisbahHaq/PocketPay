using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inventory.Application.Abstractions;
using Inventory.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Infrastructure.Services;

/// <summary>
/// Issues HS256 JWT access tokens.
/// </summary>
/// <remarks>
/// <para>
/// Symmetric signing is the right choice for a single API. It is simpler and faster to
/// verify than RSA, and asymmetric keys only start to pay off once a second service
/// needs to validate tokens without holding the signing key.
/// </para>
/// <para>
/// The signing key is validated as HMAC-SHA256 needs at least 256 bits. This fails at
/// startup rather than silently producing tokens that are weak or ambiguous.
/// </para>
/// </remarks>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider dateTime) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken CreateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = dateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        // Short claim names rather than the long ones in the default JwtSecurityTokenHandler
        // mapping. They are smaller, and they stop Identity's inbound claim mapping from
        // rewriting "sub" into a legacy claim URI that downstream code then has to know about.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
        };

        // Roles are added verbatim from the database and nothing else. There is
        // deliberately no fallback role: defaulting a token to "Staff" would mean a
        // user whose roles were stripped could still pass every Staff-gated endpoint
        // until the token expired.
        claims.AddRange(roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}