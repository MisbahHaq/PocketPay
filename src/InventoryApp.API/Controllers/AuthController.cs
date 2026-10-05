using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryApp.API.Controllers;

/// <summary>
/// Thin HTTP adapter over <see cref="IAuthService"/>: no branching, no token logic, no data access.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    IAuthService authService,
    ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Provisions a user with a starting role. Restricted by policy, not by a role string.</summary>
    [HttpPost("register")]
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await authService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), new { id = user.Id }, user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Rotates the presented refresh token and returns a fresh pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.RefreshAsync(request, cancellationToken));

    /// <summary>
    /// Anonymous by design: a client must be able to end its session even after its access token has
    /// expired, which is exactly when logout matters most. Possession of the refresh token is the
    /// authorisation. Returns 204 whether or not the token existed, so it leaks nothing.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me() =>
        Ok(new CurrentUserResponse(
            currentUser.UserId ?? string.Empty,
            currentUser.Email ?? string.Empty,
            currentUser.Roles));
}