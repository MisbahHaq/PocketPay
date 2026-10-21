using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Application.Auth;
using WalletPlatform.Application.Auth.Login;
using WalletPlatform.Application.Auth.Refresh;
using WalletPlatform.Application.Auth.RegisterUser;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(
    RegisterUserHandler registerUserHandler,
    LoginHandler loginHandler,
    RefreshTokenHandler refreshTokenHandler) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<RegisterUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await registerUserHandler.HandleAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await loginHandler.HandleAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await refreshTokenHandler.HandleAsync(request, cancellationToken);
        return Ok(response);
    }
}
