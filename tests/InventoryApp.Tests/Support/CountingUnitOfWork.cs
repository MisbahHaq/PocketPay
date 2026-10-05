using InventoryApp.Application.Abstractions;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Tests.Support;

/// <summary>
/// Records how many times the service under test committed. Preferred over mocking
/// IUnitOfWork because a mock of a Task-returning method either discards the task or needs
/// Moq's asynchronous verification helpers for no extra insight.
/// </summary>
/// <remarks>
/// The DbSets are declared and never used: this fake exists for the auth services, which commit but
/// do not read. A use-case that needed them would need a real context, which is what the
/// integration tests provide.
/// </remarks>
internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public DbSet<Product> Products => throw new NotSupportedException(
        "This fake only counts commits. Use the integration tests for anything that queries.");

    public DbSet<Category> Categories => throw new NotSupportedException(
        "This fake only counts commits. Use the integration tests for anything that queries.");

    public void ExpectRowVersion(Product product, byte[] rowVersion) => throw new NotSupportedException(
        "Concurrency needs a real change tracker. Use the integration tests for this.");

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}