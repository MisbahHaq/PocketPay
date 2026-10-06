using Inventory.Application.Abstractions;
using Inventory.Application.Common;
using Inventory.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Auth.Commands;

/// <summary>Exchanges a refresh token for a new token pair, rotating the refresh token.</summary>
public sealed record RefreshTokenCommand(RefreshTokenRequest Request) : IRequest<TokenResponse>;

/// <summary>
/// Rotating refresh, with replay detection.
/// </summary>
/// <remarks>
/// This is the highest-value security code in the system, so the flow is explicit:
///
/// <list type="number">
///   <item>Look the token up <b>by hash</b>. The raw value never reaches the database.</item>
///   <item>Unknown, revoked or expired token → 401, identical response in all three cases
///   so the endpoint cannot be used as an oracle.</item>
///   <item>A revoked-but-not-expired token being presented is a <b>replay</b>: revoke the
///   entire family and reject. We cannot distinguish a thief replaying a stolen token
///   from a client with two tabs racing, so we assume compromise. Legit users pay one
///   re-login; an attacker loses the whole chain.</item>
///   <item>Otherwise issue a successor in the same family and revoke this one.</item>
/// </list>
/// </remarks>
public sealed class RefreshTokenCommandHandler(
    IRefreshTokenStore refreshTokenStore,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTime,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    public async Task<TokenResponse> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = dateTime.UtcNow;
        var presentedHash = RefreshToken.HashToken(command.Request.RefreshToken);

        var stored = await refreshTokenStore.FindByHashAsync(presentedHash, cancellationToken);

        if (stored is null)
        {
            logger.LogWarning("Refresh presented for an unknown token");
            throw Invalid();
        }

        if (stored.IsExpired(now))
        {
            logger.LogWarning("Expired refresh presented for user {UserId}", stored.UserId);
            throw Invalid();
        }

        if (stored.IsReplayed)
        {
            // A token that was already rotated has come back. Assume it was captured and
            // burn every descendant of this chain, including sessions the attacker opened.
            logger.LogError(
                "Refresh token reuse detected for user {UserId}; revoking family {FamilyId}",
                stored.UserId,
                stored.FamilyId);

            await refreshTokenStore.RevokeFamilyAsync(
                stored.FamilyId,
                now,
                RefreshTokenRevocationReasons.ReuseDetected,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedException(
                "Session has been revoked. Please sign in again.",
                "refresh-token-reuse");
        }

        if (stored.IsRevoked)
        {
            logger.LogWarning("Revoked refresh presented for user {UserId}", stored.UserId);
            throw Invalid();
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());

        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Refresh presented for unusable account {UserId}", stored.UserId);
            throw Invalid();
        }

        var (successor, rawSuccessor) = RefreshToken.Create(
            user.Id,
            stored.FamilyId,
            now,
            lifetimeDays: (int)Math.Max(1, (stored.ExpiresAt - now).TotalDays));

        // Close the current link before adding the successor, so the chain never has a gap.
        stored.RevokedByRotation(successor.TokenHash, now);

        await refreshTokenStore.AddAsync(successor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.CreateAccessToken(user, roles);

        return new TokenResponse(
            accessToken.Value,
            accessToken.TokenType,
            (int)Math.Round((accessToken.ExpiresAt - now).TotalSeconds),
            accessToken.ExpiresAt,
            rawSuccessor,
            successor.ExpiresAt,
            CurrentUserMapper.ToResponse(user, roles));
    }

    private static UnauthorizedException Invalid() =>
        new("Refresh token is invalid or expired.", "invalid-refresh-token");
}