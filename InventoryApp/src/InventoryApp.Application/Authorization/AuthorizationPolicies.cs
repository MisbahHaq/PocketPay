namespace InventoryApp.Application.Authorization;

/// <summary>
/// Names of the authorization policies. Endpoints declare a policy rather than a
/// role, so the rule "who may do this" lives in one place (the policy registry)
/// instead of being duplicated across controllers.
/// </summary>
public static class AuthorizationPolicies
{
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanApproveAdjustments = nameof(CanApproveAdjustments);
    public const string CanRecordMovements = nameof(CanRecordMovements);
}
