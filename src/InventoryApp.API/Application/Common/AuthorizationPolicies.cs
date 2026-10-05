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
}
