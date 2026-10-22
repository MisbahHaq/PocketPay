using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(token => token.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            Items.Where(token => token.FamilyId == familyId).ToList());

    public void Add(RefreshToken refreshToken) => Items.Add(refreshToken);
}
