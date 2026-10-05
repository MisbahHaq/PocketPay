using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Abstractions;

/// <summary>
/// Commits every change made through this abstraction in one transaction, so that e.g. a
/// soft-deleted product and its audit stamps land together or not at all.
/// </summary>
/// <remarks>
/// The catalogue exposes <see cref="DbSet{TEntity}"/>s rather than purpose-built repositories. In
/// EF Core a <c>DbSet</c> already is the repository - it composes, tracks and translates - so a
/// wrapper per entity would be a pass-through with no behaviour in it, and the catalogue needs
/// LINQ composition (filters, paging, projections) that a hand-written repository interface would
/// only get in the way of. The trade-off, stated plainly: this interface is EF-flavoured, so it is
/// the seam that would have to move if persistence were ever replaced. Keeping it in
/// Application/Abstractions still means the dependency arrow points inwards, which is the part
/// that actually matters.
/// </remarks>
public interface IUnitOfWork
{
    DbSet<Product> Products { get; }

    DbSet<Category> Categories { get; }

    /// <summary>
    /// Declares the row version the caller claims to have read, so that EF Core's generated
    /// <c>UPDATE</c> matches on it and reports zero rows affected if the row has moved on.
    /// </summary>
    /// <remarks>
    /// This cannot be done by assigning the property. Assigning sets the <em>current</em> value - the
    /// one written back - while the original value, the one EF puts in the <c>WHERE</c> clause,
    /// stays as loaded. Doing it by assignment therefore compiles, looks right, and silently
    /// disables the concurrency check. EF exposes only the Entry API for this, so it is wrapped
    /// here rather than spread through the services as <c>((DbContext)uow).Entry(...)</c> casts.
    /// </remarks>
    void ExpectRowVersion(Product product, byte[] rowVersion);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}