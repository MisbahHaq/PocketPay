using Inventory.Application.Abstractions;
using Inventory.Domain.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Services;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class UnitOfWork(InventoryDbContext context, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public IReadOnlyList<IDomainEvent> CollectPendingDomainEvents() =>
        [.. context.ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .SelectMany(entity => entity.DomainEvents)];

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var attempts = 0;

        while (true)
        {
            attempts++;

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await operation(cancellationToken);

                // The delegate is expected to call SaveChanges itself, because only the
                // caller knows what "the work" consists of. Committing here is what makes
                // those saves atomic.
                await transaction.CommitAsync(cancellationToken);

                return;
            }
            catch (DbUpdateConcurrencyException exception) when (attempts < maxRetries)
            {
                logger.LogWarning(
                    exception,
                    "Concurrency conflict on attempt {Attempt} of {MaxRetries}; retrying",
                    attempts,
                    maxRetries);

                await SafeRollbackAsync(transaction);

                // The change tracker still holds entities mutated by the failed attempt.
                // Reloading discards those in-memory edits, so the retry re-reads current
                // database state and re-evaluates its stock checks instead of retrying the
                // stale numbers - which is what closes the read-modify-write race.
                context.ChangeTracker.Clear();

                // Each attempt is a new transaction, so two concurrent attempts cannot
                // deadlock against each other's row locks indefinitely.
                await Task.Delay(BackoffDelay(attempts), cancellationToken);
            }
            catch (DbUpdateException exception) when (IsTransient(exception) && attempts < maxRetries)
            {
                logger.LogWarning(
                    exception,
                    "Transient database failure on attempt {Attempt} of {MaxRetries}; retrying",
                    attempts,
                    maxRetries);

                await SafeRollbackAsync(transaction);

                context.ChangeTracker.Clear();

                await Task.Delay(BackoffDelay(attempts), cancellationToken);
            }
            catch
            {
                await SafeRollbackAsync(transaction);
                throw;
            }
        }
    }

    /// <summary>Exponential backoff with jitter, so retries from several nodes do not align.</summary>
    private static TimeSpan BackoffDelay(int attempt)
    {
        var baseMilliseconds = Math.Min(50 * Math.Pow(2, attempt - 1), 1_000);

        return TimeSpan.FromMilliseconds(baseMilliseconds + Random.Shared.Next(0, 25));
    }

    private static bool IsTransient(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { IsTransient: true };

    private async Task SafeRollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (Exception rollbackFailure)
        {
            // The transaction is already broken at this point, so a failed rollback
            // tells us nothing new. Swallowing it keeps the original, meaningful
            // exception as the one that propagates.
            logger.LogDebug(rollbackFailure, "Rollback failed; the connection will be discarded");
        }
    }
}