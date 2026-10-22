using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeWalletRepository : IWalletRepository
{
    public List<Wallet> Items { get; } = [];

    public Task<Wallet?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(wallet => wallet.UserId == userId));

    public Task<Wallet?> FindByIdAsync(Guid walletId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(wallet => wallet.Id == walletId));

    public void Add(Wallet wallet) => Items.Add(wallet);
}
