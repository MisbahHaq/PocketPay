namespace Inventory.Application.Abstractions;

/// <summary>
/// Commits the current unit of work, optionally under a database transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drains the domain events raised by entities touched in this unit of work.
    /// </summary>
    /// <remarks>
    /// Implemented by inspecting the EF change tracker, which is why it lives on the
    /// unit of work rather than on the entities: handlers must not have to know which
    /// aggregates a given use case happens to touch.
    /// </remarks>
    IReadOnlyList<Domain.Common.IDomainEvent> CollectPendingDomainEvents();

    /// <summary>
    /// Runs <paramref name="operation"/> inside a single database transaction, retrying
    /// on transient and concurrency failures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every multi-step stock operation must go through here. Receiving a purchase order
    /// line writes a movement, updates the stock balance and stamps the order line - three
    /// statements that are only correct together. Without a transaction a crash between
    /// them leaves the ledger and the balance disagreeing, and there is no way to tell
    /// which one is right.
    /// </para>
    /// <para>
    /// Retries matter because optimistic concurrency turns a lost update into an
    /// exception. A browser that fires two requests, or a user who double-clicks, will
    /// hit that constantly. Retrying is safe only because the whole delegate is replayed
    /// and the stock checks are re-evaluated against fresh state - the classic
    /// read-modify-write race is closed by re-reading inside the retry.
    /// </para>
    /// </remarks>
    /// <param name="operation">The work to perform. Must be idempotent with respect to the data it reads.</param>
    /// <param name="maxRetries">Attempts before the conflict is surfaced to the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An open database transaction, abstracted so Application never references
/// <c>IDbContextTransaction</c>.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}