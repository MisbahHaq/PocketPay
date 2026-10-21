using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    void Add(RefreshToken refreshToken);
}
