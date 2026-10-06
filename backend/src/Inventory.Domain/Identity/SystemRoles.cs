namespace Inventory.Domain.Identity;

/// <summary>
/// The three roles in the system, and what each is allowed to do.
/// </summary>
/// <remarks>
/// <para>
/// The split follows the physical trust boundary in a warehouse, not a generic
/// RBAC/ABAC matrix:
/// </para>
/// <list type="bullet">
///   <item><b>Admin</b> - owns configuration and user accounts. The only role that can
///   delete or create users, and the only role that can hard-delete catalog rows.</item>
///   <item><b>Manager</b> - owns commercial decisions: approves purchase orders, confirms
///   sales orders, books stock adjustments. Can change catalog and pricing.</item>
///   <item><b>Staff</b> - owns physical work: receives goods, issues stock, records
///   transfers. Can move stock but cannot approve it, and cannot change prices or
///   reorder levels. Read access across the catalog.</item>
/// </list>
/// <para>
/// Keeping "record" and "approve" apart is the control that makes the audit log
/// worth having: no single account can both create a movement and sign it off.
/// </para>
/// </remarks>
public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, Staff];
}