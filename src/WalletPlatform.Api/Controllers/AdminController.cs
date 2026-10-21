using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Admin.GetUsersPage;
using WalletPlatform.Application.Common;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin")]
public sealed class AdminController(GetUsersPageHandler getUsersPageHandler) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType<PagedResult<UserSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserSummary>>> GetUsers(
        [FromQuery] GetUsersPageQuery query,
        CancellationToken cancellationToken)
    {
        var response = await getUsersPageHandler.HandleAsync(query, cancellationToken);
        return Ok(response);
    }
}
