namespace InventoryApp.Application.Abstractions;

/// <summary>
/// Commits every change made through the repository abstractions in one transaction, so that
/// revoking a rotated refresh token and inserting its successor either both happen or neither does.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
