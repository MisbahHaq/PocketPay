using Inventory.Application.Abstractions;
using Inventory.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills in audit and soft-delete columns automatically on every tracked change.
/// </summary>
/// <remarks>
/// <para>
/// Implemented as an interceptor rather than inside each handler, and rather than as an
/// override on <c>SaveChanges</c>, for one practical reason: a handler that forgot to
/// stamp <c>UpdatedAt</c> would produce a subtly wrong audit trail, and nothing at the
/// call site would look wrong. One interception point makes omission impossible.
/// </para>
/// <para>
/// Values come from injected providers, never from the entity being written, so a
/// request cannot forge who performed an action.
/// </para>
/// </remarks>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUserService currentUser,
    IDateTimeProvider dateTime)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = dateTime.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                default:
                    break;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State is EntityState.Deleted)
            {
                // A hard delete of a soft-deletable entity is a mistake: convert it into
                // a tombstone row. This guarantees "we never hard-delete history" even
                // if some future code path calls Remove() on one of these.
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedBy = userId;
            }
        }
    }
}