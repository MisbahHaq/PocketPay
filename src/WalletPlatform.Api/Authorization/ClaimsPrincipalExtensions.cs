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
}
