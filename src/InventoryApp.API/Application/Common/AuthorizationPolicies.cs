namespace InventoryApp.Application.Common;

/// <summary>
/// Authorization policy names. Controllers reference these constants instead of role names so a
/// role change is a one-line edit here rather than a search across every controller.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Create and deactivate users, assign roles. Admin only.</summary>
    public const string CanManageUsers = nameof(CanManageUsers);

    /// <summary>Approve stock adjustments that move inventory outside normal bounds.</summary>
    public const string CanApproveAdjustments = nameof(CanApproveAdjustments);

    /// <summary>Record stock movements (receipts, issues, transfers).</summary>
    public const string CanRecordMovements = nameof(CanRecordMovements);

    /// <summary>
    /// Create and edit products and categories. Admin and Manager, because editing the catalogue is
    /// part of the day job; Staff consume the catalogue but do not author it.
    /// </summary>
    public const string CanManageCatalog = nameof(CanManageCatalog);

    /// <summary>
    /// Remove a product, or bring a removed one back. Admin only, and deliberately one policy for
    /// both: a restore is as destructive as a delete, because it puts a product back in front of
    /// buyers who were already told it was gone.
    /// </summary>
    public const string CanDeleteCatalog = nameof(CanDeleteCatalog);
}
