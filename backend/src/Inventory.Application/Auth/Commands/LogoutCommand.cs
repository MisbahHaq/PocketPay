using Inventory.Application.Abstractions;
using Inventory.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Auth.Commands;

/// <summary>Revokes a refresh token. Idempotent by design.</summary>
public sealed record LogoutCommand(LogoutRequest Request) : IRequest;

/// <summary>
/// Signs a client out by revoking its refresh token.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous on purpose. A user whose access token has already expired cannot send a
/// valid bearer token, and a logout that fails for that reason leaves a session the
/// user believes is closed. Revoking a token the caller does not own is harmless - the
/// token is a 512-bit random value, so it is unguessable.
/// </para>
/// <para>
/// Always returns 204, even when the token was already revoked or unknown. Reporting
/// "this token did not exist" would turn the endpoint into a token oracle.
/// </para>
/// </remarks>
public sealed class LogoutCommandHandler(
    IRefreshTokenStore refreshTokenStore,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTime,
    ILogger<LogoutCommandHandler> logger)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = dateTime.UtcNow;
        var hash = RefreshToken.HashToken(command.Request.RefreshToken);

        var stored = await refreshTokenStore.FindByHashAsync(hash, cancellationToken);

        if (stored is null)
        {
            logger.LogInformation("Logout presented for an unknown token; nothing to revoke");
            return;
        }

        // Revoke the whole family so every tab/device signed in from this login chain dies too.
        await refreshTokenStore.RevokeFamilyAsync(
            stored.FamilyId,
            now,
            RefreshTokenRevocationReasons.Logout,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Signed out user {UserId} and revoked refresh family {FamilyId}", stored.UserId, stored.FamilyId);
    }
}