using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using InventoryApp.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryApp.API.RateLimiting;

/// <summary>
/// Fixed-window rate limits on the token endpoints, using the framework's
/// built-in limiter (ASP.NET Core 7+) rather than a third-party package.
///
/// The two endpoints are throttled at different levels on purpose:
///   * login is limited per client IP, which protects the server's CPU from a
///     password-spraying run across many accounts;
///   * per-account throttling is left to Identity's lockout counter, because
///     only the application layer can safely compare a password.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>Attempts per minute, per IP, on /login.</summary>
    private const int LoginPermitLimit = 5;

    /// <summary>Calls per minute, per user (or per IP when anonymous), on /refresh.</summary>
    private const int RefreshPermitLimit = 30;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = WriteRejectionAsync;

        options.AddPolicy(RateLimitPolicyNames.Login, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(httpContext),
                _ => CreateWindow(LoginPermitLimit)));

        // Placed after UseAuthentication(), so a rotated access token identifies
        // the partition. An anonymous caller falls back to the IP.
        options.AddPolicy(RateLimitPolicyNames.Refresh, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirstValue(AuthClaimTypes.UserId) ?? GetClientIp(httpContext),
                _ => CreateWindow(RefreshPermitLimit)));
    }

    private static FixedWindowRateLimiterOptions CreateWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    private static string GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static ValueTask WriteRejectionAsync(
        OnRateLimiterRejectedContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        var retryAfterSeconds = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var retryAfter)
                ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                : (int)Window.TotalSeconds;

        httpContext.Response.Headers.RetryAfter =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Detail = "Too many attempts. Please wait before trying again.",
            Instance = httpContext.Request.Path,
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        problemDetails.Extensions["retryAfterSeconds"] = retryAfterSeconds;

        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        return problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });
    }
}
