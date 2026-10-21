using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class RefreshTokenRepository(WalletDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);
}
