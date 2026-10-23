using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class TransferRepository(WalletDbContext dbContext) : ITransferRepository
{
    public Task AddAsync(Transfer transfer, CancellationToken cancellationToken = default)
    {
        dbContext.Transfers.Add(transfer);
        return Task.CompletedTask;
    }

    public Task<Transfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Transfers
            .Where(t => t.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<Transfer>> GetBySourceWalletIdAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        dbContext.Transfers
            .Where(t =>
                t.SourceWalletId == walletId &&
                t.CreatedAt >= from &&
                t.CreatedAt <= to)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Transfer>)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    public Task<long> GetBySourceWalletIdCountAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        dbContext.Transfers
            .Where(t =>
                t.SourceWalletId == walletId &&
                t.CreatedAt >= from &&
                t.CreatedAt <= to)
            .LongCountAsync(cancellationToken);

    public Task<IReadOnlyList<Transfer>> GetByDestinationWalletIdAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        dbContext.Transfers
            .Where(t =>
                t.DestinationWalletId == walletId &&
                t.CreatedAt >= from &&
                t.CreatedAt <= to)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Transfer>)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    public Task<long> GetByDestinationWalletIdCountAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        dbContext.Transfers
            .Where(t =>
                t.DestinationWalletId == walletId &&
                t.CreatedAt >= from &&
                t.CreatedAt <= to)
            .LongCountAsync(cancellationToken);

    public Task<long> GetCompletedOutgoingSumAsync(
        Guid walletId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        dbContext.Transfers
            .Where(t =>
                t.SourceWalletId == walletId &&
                t.Status == TransferStatus.Completed &&
                t.CreatedAt >= since)
            .SumAsync(t => t.Amount.MinorUnits + t.Fee.MinorUnits, cancellationToken);
}