using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.API.Controllers;

/// <summary>
/// One protected endpoint per authorization policy, so each rule can be exercised end to end
/// without building modules 2 and 3 first.
/// </summary>
[ApiController]
[Route("api/samples")]
[Produces("application/json")]
public sealed class AuthorizationSamplesController(ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Admin only. Stands in for POST /api/users, PUT /api/users/{id}/role.</summary>
    [HttpGet("manage-users")]
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public ActionResult<object> ManageUsers() => Ok(Describe(AuthorizationPolicies.CanManageUsers));

    /// <summary>Admin or Manager. Stands in for POST /api/stock-adjustments/{id}/approve.</summary>
    [HttpGet("approve-adjustments")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveAdjustments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public ActionResult<object> ApproveAdjustments() => Ok(Describe(AuthorizationPolicies.CanApproveAdjustments));

    /// <summary>Every role. Stands in for POST /api/stock-movements.</summary>
    [HttpGet("record-movements")]
    [Authorize(Policy = AuthorizationPolicies.CanRecordMovements)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public ActionResult<object> RecordMovements() => Ok(Describe(AuthorizationPolicies.CanRecordMovements));

    private object Describe(string policy) => new
    {
        Policy = policy,
        UserId = currentUser.UserId,
        Email = currentUser.Email,
        Roles = currentUser.Roles,
        Message = $"Authorized by policy '{policy}'."
    };
}