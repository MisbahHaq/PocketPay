using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Auth.Refresh;

public sealed class RefreshTokenHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const string InvalidTokenMessage = "Invalid refresh token.";

    public async Task<AuthResponse> HandleAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var current = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedException(InvalidTokenMessage);

        if (!current.IsActive(now))
        {
            if (current.IsRevoked)
            {
                // A revoked token being presented again means it leaked: revoke every
                // still-active token in the same family so the attacker loses access too.
                var family = await refreshTokens.GetFamilyAsync(current.FamilyId, cancellationToken);
                foreach (var familyToken in family.Where(token => token.IsActive(now)))
                {
                    familyToken.Revoke(now);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw new UnauthorizedException(InvalidTokenMessage);
        }

        var user = await users.FindByIdAsync(current.UserId, cancellationToken)
            ?? throw new UnauthorizedException(InvalidTokenMessage);

        var accessToken = tokenService.CreateAccessToken(user, now);
        var refreshToken = tokenService.CreateRefreshToken(now);
        var replacement = RefreshToken.Create(
            user.Id,
            refreshToken.TokenHash,
            current.FamilyId,
            now,
            refreshToken.ExpiresAtUtc);

        current.Revoke(now);
        current.MarkReplacedBy(replacement.Id);
        refreshTokens.Add(replacement);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.RawValue,
            refreshToken.ExpiresAtUtc);
    }
}
