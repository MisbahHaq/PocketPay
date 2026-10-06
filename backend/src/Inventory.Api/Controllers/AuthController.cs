using Asp.Versioning;
using Inventory.Application.Auth;
using Inventory.Application.Common;
using Inventory.Application.Auth.Commands;
using Inventory.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Inventory.Api.Controllers;

/// <summary>Authentication and account endpoints.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(ISender sender) : ApiControllerBase
{
    /// <summary>Exchanges credentials for an access token and a refresh token.</summary>
    /// <response code="200">Authenticated. Token pair and user profile returned.</response>
    /// <response code="401">Credentials are wrong, the account is locked, or it is deactivated.</response>
    /// <response code="422">The request body failed validation.</response>
    /// <response code="429">Too many attempts from this address.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new LoginCommand(request), cancellationToken));

    /// <summary>Rotates the refresh token and issues a new token pair.</summary>
    /// <response code="200">Refreshed. The previous refresh token is now revoked.</response>
    /// <response code="401">
    /// The token is unknown, expired, or revoked. A revoked token that was already
    /// rotated has been replayed, and the entire token family has been revoked as a result.
    /// </response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RefreshTokenCommand(request), cancellationToken));

    /// <summary>Revokes a refresh token and every token issued from the same sign-in.</summary>
    /// <response code="204">Signed out, or nothing to do. Always 204, by design.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(request), cancellationToken);
        return NoContent();
    }

    /// <summary>Returns the authenticated user's profile and current roles.</summary>
    /// <remarks>
    /// Roles are read from the database rather than from the token, so a role change
    /// takes effect immediately instead of after the access token expires.
    /// </remarks>
    /// <response code="200">The current user.</response>
    /// <response code="401">No valid access token, or the account is deactivated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetCurrentUserQuery(), cancellationToken));

/// <summary>Creates a user account. Admin only - there is no self-registration.</summary>
    /// <response code="201">Created. The new user's profile and role.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Authenticated but not an administrator.</response>
    /// <response code="409">An account with that email already exists.</response>
    /// <response code="422">The request failed validation.</response>
    [HttpPost("users")]
    [Authorize(Policy = AuthorizationPolicies.ManageUsers)]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CurrentUserResponse>> RegisterUser(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new RegisterUserCommand(request), cancellationToken);

        // 201, not 200: this created a resource, and clients that distinguish the two
        // treat a create as non-idempotent. No Location header, because Phase 1 exposes
        // no GET for a single user; adding one next to this would be a dead link.
        return StatusCode(StatusCodes.Status201Created, created);
    }
}

/// <summary>Marker type used only to attach OpenAPI schema types to error responses.</summary>
public sealed class ProblemResponse : ProblemDetails
{
}