using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class WalletRepository(WalletDbContext dbContext) : IWalletRepository
{
    public Task<Wallet?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Wallets.SingleOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken);

    public void Add(Wallet wallet) => dbContext.Wallets.Add(wallet);
}
