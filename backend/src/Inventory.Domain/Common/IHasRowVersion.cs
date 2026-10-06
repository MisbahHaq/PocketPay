namespace Inventory.Domain.Common;

/// <summary>
/// Soft delete. Implemented as an interface plus a global EF Core query filter so
/// that forgetting to filter is impossible rather than merely discouraged.
/// </summary>
/// <remarks>
/// <para>
/// Hard deletes are wrong for most of this system. A stock movement is a financial
/// record; a purchase order may be referenced by a received movement; an audit log
/// entry must outlive the row it describes. Deleting them would either break the
/// referential history or cascade silently.
/// </para>
/// <para>
/// The one deliberate exception is that users and roles are still hard-deleted -
/// removing a login must actually revoke it - and that is why this is opt-in per
/// entity instead of applied globally to the base class.
/// </para>
/// </remarks>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }

    Guid? DeletedBy { get; set; }
}

/// <summary>
/// Optimistic concurrency token.
/// </summary>
/// <remarks>
/// <para>
/// Maps to PostgreSQL's <c>xmin</c> system column, so the database - not the
/// application - decides whether a row changed. We surface it as <see cref="uint"/>
/// and keep it behind this interface so the storage strategy stays a
/// one-file decision.
/// </para>
/// <para>
/// This is the mechanism that prevents overselling. Two operators issue against the
/// same line at the same moment; both read version 41255, both compute "enough
/// stock"; without this the second write would silently succeed and drive the
/// balance negative. The loser gets <c>DbUpdateConcurrencyException</c>, which the
/// API surfaces as <c>409 Conflict</c> and the UI turns into "stock changed, reload".
/// </para>
/// <para>
/// Known caveat: PostgreSQL rewrites <c>xmin</c> when a row is frozen by
/// autovacuum. In practice this affects long-idle rows rather than hot ones, and
/// the failure mode is a spurious conflict (safe) rather than a missed conflict
/// (unsafe). See README, "Optimistic concurrency".
/// </para>
/// </remarks>
public interface IHasRowVersion
{
    uint RowVersion { get; set; }
}