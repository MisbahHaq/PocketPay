using WalletPlatform.Application.Abstractions.Persistence;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public int TransactionCount { get; private set; }

    public Func<Exception>? SaveFailure { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        if (SaveFailure is not null)
        {
            throw SaveFailure();
        }

        return Task.FromResult(1);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return await operation(cancellationToken);
    }
}
