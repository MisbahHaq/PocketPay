using InventoryApp.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.API.Controllers;

/// <summary>
/// Deliberately minimal endpoints, one per authorization policy, so each policy
/// can be exercised from Swagger UI without the inventory module existing yet.
/// They will be replaced by the real inventory endpoints as later modules land.
/// </summary>
[ApiController]
[Route("api/samples/policies")]
public sealed class PolicySampleController : ControllerBase
{
    /// <summary>Requires the CanManageUsers policy (Admin or Manager).</summary>
    [HttpGet("manage-users")]
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [ProducesResponseType<PolicyProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public ActionResult<PolicyProbeResponse> ManageUsers() => Ok(Describe(
        AuthorizationPolicies.CanManageUsers,
        "You may manage users."));

    /// <summary>Requires the CanApproveAdjustments policy (Admin or Manager).</summary>
    [HttpGet("approve-adjustments")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveAdjustments)]
    [ProducesResponseType<PolicyProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public ActionResult<PolicyProbeResponse> ApproveAdjustments() => Ok(Describe(
        AuthorizationPolicies.CanApproveAdjustments,
        "You may approve stock adjustments."));

    /// <summary>Requires the CanRecordMovements policy (Admin, Manager or Staff).</summary>
    [HttpGet("record-movements")]
    [Authorize(Policy = AuthorizationPolicies.CanRecordMovements)]
    [ProducesResponseType<PolicyProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public ActionResult<PolicyProbeResponse> RecordMovements() => Ok(Describe(
        AuthorizationPolicies.CanRecordMovements,
        "You may record stock movements."));

    private PolicyProbeResponse Describe(string policy, string message) => new(
        policy,
        message,
        User.Identity?.Name ?? string.Empty,
        User.Claims
            .Where(claim => claim.Type == Application.Auth.AuthClaimTypes.Role)
            .Select(claim => claim.Value)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray());
}

/// <summary>Echoes back what the current token resolved to.</summary>
public sealed record PolicyProbeResponse(
    string Policy,
    string Message,
    string UserId,
    IReadOnlyList<string> Roles);
