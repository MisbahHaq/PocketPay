using Inventory.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Domain.Identity;

/// <summary>
/// The application user. Extends ASP.NET Identity's user with the two fields a
/// warehouse operator actually needs: a display name and an active flag.
/// </summary>
/// <remarks>
/// Keyed by <see cref="Guid"/> rather than Identity's default string. Identity fully
/// supports a custom key type (<c>IdentityUser&lt;Guid&gt;</c>), and taking it keeps
/// every foreign key in the system the same shape: user ids drop straight into
/// <c>CreatedBy</c>, <c>ApprovedBy</c> and <c>PerformedBy</c> without a conversion,
/// which matters because those columns are the backbone of the audit trail.
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>, IHasRowVersion
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// An inactive user keeps their row, roles and audit trail but cannot obtain a
    /// token. We never delete users: deleting one would orphan every stock movement
    /// and order they ever touched, which is precisely the history we must preserve.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Maps to PostgreSQL's <c>xmin</c> and guards the user row.
    /// </summary>
    /// <remarks>
    /// Two devices signing in at once both update <see cref="LastLoginAt"/>; without a
    /// concurrency token the later write silently discards the earlier one. More
    /// importantly, this is the same mechanism the inventory tables will use, so having
    /// it live on the user row from the start means the pattern is exercised by
    /// ordinary traffic rather than first discovered during a stock incident.
    /// </remarks>
    public uint RowVersion { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}