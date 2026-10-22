using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class WalletRepository(WalletDbContext dbContext) : IWalletRepository
{
    public Task<Wallet?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Wallets.SingleOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken);

    public Task<Wallet?> FindByIdAsync(Guid walletId, CancellationToken cancellationToken) =>
        dbContext.Wallets.SingleOrDefaultAsync(wallet => wallet.Id == walletId, cancellationToken);

    public void Add(Wallet wallet) => dbContext.Wallets.Add(wallet);
}
