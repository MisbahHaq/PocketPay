using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Exceptions;

namespace WalletPlatform.Api.Middleware;

/// <summary>
/// Last line of defense: converts any unhandled exception into an RFC 9457
/// (formerly RFC 7807) ProblemDetails response without leaking internals.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ConflictException exception)
        {
            logger.LogInformation("Request conflict: {Message}", exception.Message);
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Conflict.",
                exception.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10");
        }
        catch (UnauthorizedException exception)
        {
            logger.LogInformation("Unauthorized request: {Message}", exception.Message);
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized.",
                exception.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.2");
        }
        catch (NotFoundException exception)
        {
            logger.LogInformation("Resource not found: {Message}", exception.Message);
            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "Not found.",
                exception.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }
        catch (LedgerException exception)
        {
            // A ledger rule was violated (e.g. an unbalanced entry). This is a client
            // problem, not a server fault, so it must not surface as a 500.
            logger.LogInformation("Ledger rule violated: {Message}", exception.Message);
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Ledger rule violated.",
                exception.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                detail: null,
                "https://tools.ietf.org/html/rfc9110#section-15.6.1");
        }
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string? detail,
        string type)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                "Response already started; unable to write a {StatusCode} problem response.",
                statusCode);
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }
}
