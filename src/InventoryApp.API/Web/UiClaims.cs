using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InventoryApp.Application.Common;
using InventoryApp.Application.Dtos;

namespace InventoryApp.Web;

/// <summary>
/// Builds the principal stored in the UI cookie from a freshly issued token pair.
/// </summary>
public static class UiClaims
{
    /// <summary>Refresh token, so signing out of the UI also revokes the token server-side.</summary>
    public const string RefreshToken = "ui:refresh_token";

    /// <summary>Access-token expiry, as Unix seconds. Drives the countdown on the dashboard.</summary>
    public const string SessionExpiresAt = "ui:expires_at";

    /// <summary>
    /// Reads the access token's claims into a cookie principal.
    /// </summary>
    /// <remarks>
    /// The token is decoded rather than re-read from the database because it was minted by this
    /// process moments ago from claims already in hand, so it is by construction both authentic
    /// and current. Inbound claim mapping is switched off for the same reason as in the bearer
    /// handler: the token is written under the short names in <see cref="AuthClaimTypes"/> and must
    /// not be silently rewritten to the long <c>ClaimTypes</c> URIs on the way back in.
    /// </remarks>
    public static ClaimsPrincipal FromTokenPair(TokenResponse tokens)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var token = handler.ReadJwtToken(tokens.AccessToken);

        var claims = token.Claims
            .Where(claim => claim.Type is AuthClaimTypes.UserId
                or AuthClaimTypes.Email
                or AuthClaimTypes.Name
                or AuthClaimTypes.Role)
            .ToList();

        claims.Add(new Claim(RefreshToken, tokens.RefreshToken));
        claims.Add(new Claim(
            SessionExpiresAt,
            tokens.AccessTokenExpiresAt.ToUnixTimeSeconds().ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            AuthSchemes.UiCookie,
            AuthClaimTypes.Name,
            AuthClaimTypes.Role));
    }

    /// <summary>
    /// Reads the session expiry back out of the principal, or null when the claim is missing or
    /// malformed, which the dashboard treats as "expired, sign in again".
    /// </summary>
    public static DateTimeOffset? GetSessionExpiry(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(SessionExpiresAt)?.Value;

        return long.TryParse(raw, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }
}