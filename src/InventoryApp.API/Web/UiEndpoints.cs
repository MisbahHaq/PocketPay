using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryApp.Web;

/// <summary>
/// Form-post endpoints backing the UI's sign-in, sign-up and sign-out screens.
/// </summary>
/// <remarks>
/// These are endpoints rather than component event handlers for one specific reason: only a real
/// HTTP request has a usable <see cref="HttpContext"/>, and setting or clearing an authentication
/// cookie requires one. An interactive Blazor Server circuit is long-lived and has no usable
/// HttpContext once its initial render is done, so a cookie can be neither written nor deleted from
/// inside a circuit. Keeping these screens on static server-side rendering keeps cookie writes on
/// the same thread as the HTTP response, which is where cookies are safe to manipulate.
/// </remarks>
public static class UiEndpoints
{
    /// <summary>Form field carrying the page to return to after a successful sign-in.</summary>
    private const string ReturnUrlField = "returnUrl";

    public const string LoginPage = "/login";
    public const string RegisterPage = "/register";
    public const string HomePage = "/";

    public static IEndpointRouteBuilder MapUiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ui").WithTags("UI session");

        group.MapPost("/login", SignInAsync)
            .AllowAnonymous()
            // Credential submission is the endpoint most worth throttling, so it reuses the same
            // per-IP budget as the JSON login rather than inventing a second, looser one.
            .RequireRateLimiting(RateLimitPolicies.Login)
            // Antiforgery is validated explicitly below rather than by middleware, so that a failed
            // check redirects back to the form instead of surfacing as a bare 400.
            .DisableAntiforgery();

        group.MapPost("/register", RegisterAsync)
            // The role is required here rather than by reusing AuthorizationPolicies.CanManageUsers
            // because that policy is also enforced on the JSON API, where the caller presents a bearer
            // token. Naming the scheme keeps a cookie-authenticated browser out of a bearer-only
            // endpoint, while the role itself is still read from SystemRoles, the single source of
            // truth for role names.
            .RequireAuthorization(policy =>
            {
                policy.AddAuthenticationSchemes(AuthSchemes.UiCookie);
                policy.RequireRole(SystemRoles.Admin);
            })
            .DisableAntiforgery();

        group.MapPost("/logout", SignOutAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> SignInAsync(
        [FromForm] LoginRequest request,
        [FromForm(Name = ReturnUrlField)] string? returnUrl,
        IAuthService authService,
        IAntiforgery antiforgery,
        UiNoticeService notices,
        HttpContext context)
    {
        // Checked before the credentials are examined, so a cross-site post is refused without
        // spending a password hash or a rate-limit permit.
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.Redirect(LoginPage);

        try
        {
            var tokens = await authService.LoginAsync(request, context.RequestAborted);

            await context.SignInAsync(
                AuthSchemes.UiCookie,
                UiClaims.FromTokenPair(tokens),
                new AuthenticationProperties { IsPersistent = false });

            return Results.Redirect(SafeReturnUrl(returnUrl));
        }
        catch (AppException exception)
        {
            // The exception's own wording is shown rather than a generic "sign-in failed", because
            // an Identity lockout is only actionable if the user is told the account is locked.
            notices.Set(context, exception.Message);
            return Results.Redirect(LoginPage);
        }
    }

    private static async Task<IResult> RegisterAsync(
        [FromForm] RegisterUserRequest request,
        IAuthService authService,
        IAntiforgery antiforgery,
        UiNoticeService notices,
        HttpContext context)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.Redirect(RegisterPage);

        try
        {
            var user = await authService.RegisterAsync(request, context.RequestAborted);

            notices.Set(context, $"Created {user.Email} with the {string.Join(", ", user.Roles)} role.");
            return Results.Redirect(HomePage);
        }
        catch (AppException exception)
        {
            // Identity reports password and duplicate-email failures as a flat list keyed by error
            // code rather than by field, so the messages are joined into one readable notice.
            var message = exception is ValidationFailedException validation
                ? string.Join(" ", validation.Errors.SelectMany(pair => pair.Value))
                : exception.Message;

            notices.Set(context, message);
            return Results.Redirect(RegisterPage);
        }
    }

    private static async Task<IResult> SignOutAsync(
        HttpContext context,
        IAuthService authService,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory)
    {
        // Sign-out is the one endpoint where CSRF is worth guarding explicitly: an attacker who can
        // post from another origin would otherwise be able to end a victim's session at will.
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.Redirect(LoginPage);

        // Revoking the refresh token before clearing the cookie is the part that matters: the cookie
        // is only a client-side assertion, and the refresh token is what would otherwise keep the
        // server-side session alive for its full remaining lifetime.
        var refreshToken = context.User.FindFirst(UiClaims.RefreshToken)?.Value;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await authService.LogoutAsync(new LogoutRequest(refreshToken), context.RequestAborted);
            }
            catch (AppException exception)
            {
                // Signing out is idempotent and must not be blocked by a downstream failure, so a
                // revocation problem is logged and the local session is still torn down.
                loggerFactory.CreateLogger(typeof(UiEndpoints))
                    .LogWarning(exception, "Could not revoke the refresh token during sign-out.");
            }
        }

        await context.SignOutAsync(AuthSchemes.UiCookie);

        return Results.Redirect(LoginPage);
    }

    /// <summary>
    /// Restricts the post-sign-in redirect to a same-origin path.
    /// </summary>
    /// <remarks>
    /// An absolute or protocol-relative URL would turn the sign-in page into an open redirect that
    /// bounces a freshly authenticated user onto a site of the attacker's choosing. Control characters
    /// are rejected too: the value becomes a Location header, and a crafted one must not be able to
    /// produce extra headers or a rejected response.
    /// </remarks>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl[0] == '/'
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
        && !returnUrl.Any(char.IsControl)
            ? returnUrl
            : HomePage;
}