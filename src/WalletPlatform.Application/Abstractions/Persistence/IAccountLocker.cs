using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Persistence;

/// <summary>
/// Provides pessimistic locking on ledger accounts to prevent race conditions.
/// Locks are acquired in a consistent global order (ascending account ID) to prevent deadlocks.
/// The lock is held for the duration of the current database transaction.
/// </summary>
public interface IAccountLocker
{
    /// <summary>
    /// Acquires FOR UPDATE locks on the specified ledger accounts.
    /// Accounts are locked in ascending ID order to prevent deadlocks.
    /// Call within a transaction - the lock is released when the transaction commits or rolls back.
    /// </summary>
    Task LockAsync(params Guid[] ledgerAccountIds);
}