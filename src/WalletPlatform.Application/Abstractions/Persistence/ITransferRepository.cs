using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Persistence;

public interface ITransferRepository
{
    Task AddAsync(Transfer transfer, CancellationToken cancellationToken = default);

    Task<Transfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transfer>> GetBySourceWalletIdAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<long> GetBySourceWalletIdCountAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transfer>> GetByDestinationWalletIdAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<long> GetByDestinationWalletIdCountAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<long> GetCompletedOutgoingSumAsync(
        Guid walletId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}