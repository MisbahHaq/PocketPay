using InventoryApp.Application.Common.Interfaces;
using InventoryApp.Domain.Constants;
using InventoryApp.Domain.Entities;
using InventoryApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenStore"/>.
///
/// The two state-changing operations are written as conditional UPDATEs rather
/// than entity mutations so that they are atomic in the database. That is what
/// makes rotation safe under concurrency: two simultaneous refreshes with the
/// same token cannot both succeed.
/// </summary>
internal sealed class RefreshTokenStore(ApplicationDbContext dbContext) : IRefreshTokenStore
{
    public Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens.AddAsync(token, cancellationToken);

    public async Task<bool> TryMarkRotatedAsync(
        Guid tokenId,
        Guid replacementTokenId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.RefreshTokens
            .Where(token => token.Id == tokenId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevokedReason, RefreshTokenRevocationReasons.Rotated)
                    .SetProperty(token => token.ReplacedByTokenId, (Guid?)replacementTokenId),
                cancellationToken);

        return affectedRows == 1;
    }

    public Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevokedReason, reason),
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
