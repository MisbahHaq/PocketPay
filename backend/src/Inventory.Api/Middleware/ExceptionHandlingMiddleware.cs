using System.Diagnostics;
using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Middleware;

/// <summary>
/// Converts every unhandled exception into a single, predictable error shape.
/// </summary>
/// <remarks>
/// <para>
/// Without this, one forgotten try/catch produces three different error formats - a
/// problem+json body, an HTML error page from the developer exception page, or a bare
/// 500 - and a client cannot write one error handler. Everything is RFC 7807
/// <c>ProblemDetails</c>, which is the format .NET itself has standardised on, so the
/// frontend can parse errors without knowing anything about this API.
/// </para>
/// <para>
/// The mapping from exception type to status code lives here and nowhere else. That is
/// the whole point: business code throws a meaningful exception
/// (<c>NotFoundException</c>, <c>ConflictException</c>) and never decides what HTTP
/// status that becomes.
/// </para>
/// <para>
/// Stack traces and exception messages are logged, never returned. An exception message
/// can contain a SQL fragment, a file path or a connection string, and returning it
/// hands an attacker a map.
/// </para>
/// </remarks>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Client-facing errors are not "server errors", so they log below Error.</summary>
    private static readonly Action<ILogger, string, string, string?, Exception?> LogClientError =
        LoggerMessage.Define<string, string, string?>(
            LogLevel.Warning,
            new EventId(1000, nameof(LogClientError)),
            "Client error {TraceId} on {Path}: {Message}");

    private static readonly Action<ILogger, string, string, Exception?> LogServerError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1001, nameof(LogServerError)),
            "Unhandled exception {TraceId} on {Path}");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. Not an error worth a stack trace; rethrowing lets the
            // server tear down the request quietly.
            throw;
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (status, title, code, extensions) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogServerError(logger, traceId, context.Request.Path, exception);
        }
        else
        {
            LogClientError(logger, traceId, context.Request.Path, SafeMessage(exception), exception);
        }

        // If the response has already started - a streamed or partially written body -
        // the status line is already on the wire and cannot be corrected. Abort instead
        // of appending a second document to the body.
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Response already started; cannot write a problem details body");
            context.Abort();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Reference the trace id when reporting this."
                : SafeMessage(exception),
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.io/{status}",
        };

        problem.Extensions["traceId"] = traceId;

        if (code is not null)
        {
            // Machine-readable discriminator. The frontend switches on this instead of
            // matching human-readable text that will change during a copy edit.
            problem.Extensions["code"] = code;
        }

        if (status == StatusCodes.Status401Unauthorized && !context.Response.Headers.ContainsKey("WWW-Authenticate"))
        {
            problem.Extensions["reason"] = "unauthenticated";
        }

        foreach (var extension in extensions)
        {
            problem.Extensions[extension.Key] = extension.Value;
        }

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, ProblemJsonOptions),
            context.RequestAborted);
    }

    private static (int Status, string Title, string? Code, Dictionary<string, object?> Extensions) Map(Exception exception) =>
        exception switch
        {
            ApplicationValidationException validation => (
                StatusCodes.Status422UnprocessableEntity,
                "One or more validation errors occurred.",
                "validation-failed",
                new Dictionary<string, object?> { ["errors"] = validation.Errors }),

            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found.",
                "not-found",
                new()),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "The request conflicts with the current state.",
                conflict.Code ?? "conflict",
                new()),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "The resource was modified by another request. Reload and try again.",
                "concurrency-conflict",
                new()),

            DbUpdateException dbUpdate when IsUniqueViolation(dbUpdate) => (
                StatusCodes.Status409Conflict,
                "A record with these values already exists.",
                "duplicate",
                new()),

            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                "Authentication is required.",
                unauthorized.Code ?? "unauthorized",
                new()),

            ForbiddenAccessException forbidden => (
                StatusCodes.Status403Forbidden,
                "You do not have permission to perform this action.",
                forbidden.Code ?? "forbidden",
                new()),

            Domain.Common.ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                "You do not have permission to perform this action.",
                forbidden.Code,
                new()),

            DomainException domain => (
                StatusCodes.Status422UnprocessableEntity,
                "A business rule was violated.",
                "domain-rule-violated",
                domain.Violations.Count > 0
                    ? new Dictionary<string, object?> { ["violations"] = domain.Violations }
                    : new()),

            BadHttpRequestException bad => (
                bad.StatusCode,
                "The request could not be read.",
                "bad-request",
                new()),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found.",
                "not-found",
                new()),

            ArgumentException argument => (
                StatusCodes.Status400BadRequest,
                "The request contains an invalid argument.",
                "invalid-argument",
                new()),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "internal-error",
                new()),
        };

    /// <summary>
    /// Detects a unique-index violation. The Postgres SQLSTATE 23505 is the contract;
    /// matching on the provider's exception type would not survive a provider change.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    /// <summary>
    /// Client-safe message for 4xx only. 500s never expose theirs.
    /// </summary>
    private static string? SafeMessage(Exception exception) => exception switch
    {
        DomainException domain => domain.Message,
        ArgumentException argument => argument.Message,
        _ => null,
    };

    private static readonly JsonSerializerOptions ProblemJsonOptions = new(JsonSerializerDefaults.Web);
}