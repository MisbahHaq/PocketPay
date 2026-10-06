namespace Inventory.Application.Common;

/// <summary>
/// Named authorization policies, referenced from <c>[Authorize]</c> as strings.
/// </summary>
/// <remarks>
/// Roles alone are too coarse for an inventory system: "can record a movement" and
/// "can approve a movement" are different duties, and separating them is what makes
/// the audit trail meaningful. Policies give us a place to express that split, and a
/// single constant to change when it does. Using <c>[Authorize(Roles = "Admin,Manager")]</c>
/// inline would scatter role lists across controllers and make that change a sweep.
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>Create and deactivate user accounts, assign roles.</summary>
    public const string ManageUsers = "inventory:manage-users";

    /// <summary>Create, edit and deactivate catalogue data (products, categories, suppliers, warehouses).</summary>
    public const string ManageCatalog = "inventory:manage-catalog";

    /// <summary>Hard-delete catalogue rows. Reserved to Admin because a delete is unrecoverable.</summary>
    public const string DeleteCatalog = "inventory:delete-catalog";

    /// <summary>Record a stock movement (receive, issue, transfer, adjust).</summary>
    public const string RecordMovements = "inventory:record-movements";

    /// <summary>Approve purchase orders and sign off stock adjustments.</summary>
    public const string ApproveStock = "inventory:approve-stock";

    /// <summary>Create purchase and sales orders.</summary>
    public const string ManageOrders = "inventory:manage-orders";

    /// <summary>Cancel an order that is already in flight.</summary>
    public const string CancelOrders = "inventory:cancel-orders";

    /// <summary>View dashboards and reports.</summary>
    public const string ViewReports = "inventory:view-reports";

    /// <summary>Read and export the audit log.</summary>
    public const string ViewAuditLog = "inventory:view-audit-log";
}