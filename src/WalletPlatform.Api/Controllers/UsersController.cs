using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Users.GetCurrentUser;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(GetCurrentUserHandler getCurrentUserHandler) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var query = new GetCurrentUserQuery(User.GetUserId());
        var response = await getCurrentUserHandler.HandleAsync(query, cancellationToken);
        return Ok(response);
    }
}
