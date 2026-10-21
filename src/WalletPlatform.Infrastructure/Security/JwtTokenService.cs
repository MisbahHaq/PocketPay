using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Security;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Security;

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private const int RefreshTokenByteLength = 32;

    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user, DateTimeOffset nowUtc)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var expiresAtUtc = nowUtc.AddMinutes(_options.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public RefreshTokenValue CreateRefreshToken(DateTimeOffset nowUtc)
    {
        var rawValue = Convert.ToHexStringLower(
            RandomNumberGenerator.GetBytes(RefreshTokenByteLength));

        return new RefreshTokenValue(
            rawValue,
            HashRefreshToken(rawValue),
            nowUtc.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
