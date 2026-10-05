using InventoryApp.API.RateLimiting;
using InventoryApp.Application.Auth;
using InventoryApp.Application.Auth.Contracts;
using InventoryApp.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.API.Controllers;

/// <summary>
/// Token endpoints. Controllers stay thin: they bind, delegate to
/// <see cref="AuthService"/> and translate to HTTP status codes. Every rule
/// about credentials lives in the application layer.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Creates a user. Restricted by policy to Admin and Manager - note the
    /// service additionally refuses to grant a role outside the seeded set, so a
    /// Manager cannot mint themselves an Admin.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [ProducesResponseType<UserCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserCreatedResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await authService.RegisterAsync(request, cancellationToken);

        // Who created whom is an audit question, so record the actor alongside
        // the service's "user was created" log entry.
        logger.LogInformation(
            "User {UserId} registered by {ActorId}.",
            created.Id,
            User.Identity?.Name);

        // No Location header yet: there is no "get user by id" endpoint to point
        // at. It becomes CreatedAtAction once that endpoint exists.
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Exchanges credentials for an access token and a refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Login)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await authService.LoginAsync(request, cancellationToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Rotates the refresh token: the token that was presented is invalidated and
    /// a replacement is issued for the same session.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Refresh)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await authService.RefreshAsync(request, cancellationToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Ends the session. Always returns 204 whether or not the token was valid,
    /// so the endpoint cannot be used to discover which tokens exist.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
