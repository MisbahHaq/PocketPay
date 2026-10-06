using Inventory.Application.Abstractions;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenStore"/>.
/// </summary>
public sealed class RefreshTokenStore(InventoryDbContext context) : IRefreshTokenStore
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens.AddAsync(token, cancellationToken);

    /// <summary>
    /// Revokes a whole rotation chain in one UPDATE.
    /// </summary>
    /// <remarks>
    /// <see cref="ExecuteUpdateAsync"/> issues the statement directly in the database and
    /// bypasses the change tracker. That is intentional here: this runs on the
    /// compromise-detection path, where the family may contain many rows that are not
    /// loaded, and loading-then-saving them would be both slower and a chance to lose a
    /// concurrent revocation. The rows are not in memory, so nothing needs their tracked
    /// state.
    /// </remarks>
    public async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevokedReason, reason),
                cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevokedReason, reason),
                cancellationToken);
    }
}