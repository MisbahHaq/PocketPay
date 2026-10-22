using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WalletPlatform.Application.Common.Exceptions;

namespace WalletPlatform.Api.Authorization;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new UnauthorizedException("The access token does not identify a user.");
    }

    /// <summary>
    /// The identity to record as the creator of audit-trail records (ledger entries,
    /// reversals). Prefers the email claim, then the subject, so the value is always
    /// present for an authenticated principal.
    /// </summary>
    public static string GetRequiredUserName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedException("The access token does not identify a user.");
}
