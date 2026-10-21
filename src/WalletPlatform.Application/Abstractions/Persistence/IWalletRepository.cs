using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Persistence;

public interface IWalletRepository
{
    Task<Wallet?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Wallet wallet);
}
