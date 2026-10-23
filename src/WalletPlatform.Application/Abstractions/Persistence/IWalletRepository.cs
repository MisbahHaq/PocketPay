using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Persistence;

public interface IWalletRepository
{
    Task<Wallet?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<Wallet?> FindByIdAsync(Guid walletId, CancellationToken cancellationToken);

    /// <summary>
    /// Looks up a wallet by its 12-digit wallet number. Used by the transfer destination
    /// lookup and by the transfer service; the caller never learns whether the wallet
    /// belongs to someone else — a miss is reported as "not found" (see DECISIONS).
    /// </summary>
    Task<Wallet?> FindByWalletNumberAsync(string walletNumber, CancellationToken cancellationToken);

    void Add(Wallet wallet);
}
