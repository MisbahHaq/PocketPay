using InventoryApp.Application.Abstractions;

namespace InventoryApp.Tests.Support;

/// <summary>
/// Records how many times the service under test committed. Preferred over mocking
/// IUnitOfWork because a mock of a Task-returning method either discards the task or needs
/// Moq's asynchronous verification helpers for no extra insight.
/// </summary>
internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}