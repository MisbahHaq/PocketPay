using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using WalletPlatform.Application.Abstractions.Services;
using WalletPlatform.Application.Common;
using WalletPlatform.Api.Authorization;

namespace WalletPlatform.Api.Filters;

/// <summary>
/// MVC filter that handles idempotency keys for mutating endpoints.
/// - Validates the Idempotency-Key header is present on required endpoints
/// - Claims the key in the SAME transaction as the business operation
/// - Replays stored responses for duplicate requests
/// - Adds Idempotency-Replayed header when replaying
/// </summary>
public sealed class IdempotencyFilter(
    IIdempotencyService idempotencyService,
    IOptions<IdempotencyOptions> options) : IAsyncActionFilter
{
    private readonly IdempotencyOptions _options = options.Value;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;

        // Only apply to mutating methods on required paths
        if (!_options.RequiredPaths.Any(p => request.Path.StartsWithSegments(p)))
        {
            await next();
            return;
        }

        if (!HttpMethods.IsPost(request.Method) &&
            !HttpMethods.IsPut(request.Method) &&
            !HttpMethods.IsPatch(request.Method) &&
            !HttpMethods.IsDelete(request.Method))
        {
            await next();
            return;
        }

        // Get user ID from claims
        var userIdClaim = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst("userId")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get idempotency key from header
        var key = request.Headers[_options.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Idempotency key required",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"Missing required header '{_options.HeaderName}'",
                Instance = request.Path
            };
            problem.Extensions["code"] = "IDEMPOTENCY_KEY_REQUIRED";
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        // Read request body for hashing
        request.EnableBuffering();
        var body = request.Body;
        var bodyString = "";
        using (var reader = new StreamReader(body, leaveOpen: true))
        {
            bodyString = await reader.ReadToEndAsync();
        }
        body.Position = 0;

        var requestBody = string.IsNullOrEmpty(bodyString)
            ? new { }
            : System.Text.Json.JsonSerializer.Deserialize<object>(bodyString);

        try
        {
            var claimResult = await idempotencyService.TryClaimAsync(
                userId,
                key,
                request.Method,
                request.Path.Value ?? "",
                requestBody!);

            if (claimResult is not null)
            {
                // Replay stored response
                var replayResult = new ObjectResult(
                    string.IsNullOrEmpty(claimResult.ResponseBody)
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<object>(claimResult.ResponseBody))
                {
                    StatusCode = claimResult.StatusCode
                };

                // Add replay header
                httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                context.Result = replayResult;
                return;
            }

            // Store the claim result in HttpContext for the controller to use
            httpContext.Items["IdempotencyKey"] = key;
            httpContext.Items["IdempotencyUserId"] = userId;

            await next();

            // After action executes, store the response
            var result = context.Result;
            if (result is ObjectResult objectResult)
            {
                await idempotencyService.StoreResponseAsync(
                    userId,
                    key,
                    objectResult.StatusCode ?? 500,
                    objectResult.Value);
            }
            else if (result is StatusCodeResult statusCodeResult)
            {
                await idempotencyService.StoreResponseAsync(
                    userId,
                    key,
                    statusCodeResult.StatusCode,
                    null);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("different request payload"))
        {
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Idempotency key reused with different payload",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = ex.Message,
                Instance = request.Path
            };
            problem.Extensions["code"] = "IDEMPOTENCY_KEY_REUSED";
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }
        catch (Exception ex) when (ex.Message.Contains("already being processed"))
        {
            // Concurrent request with same key - return 409
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Idempotency key already in progress",
                Status = StatusCodes.Status409Conflict,
                Detail = "A request with this idempotency key is already being processed",
                Instance = request.Path
            };
            problem.Extensions["code"] = "IDEMPOTENCY_KEY_IN_PROGRESS";
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
        }
    }
}