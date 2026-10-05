using System.Diagnostics;
using FluentValidation;
using InventoryApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InventoryApp.API.Middleware;

/// <summary>
/// Turns any exception escaping the pipeline into an RFC 7807 ProblemDetails
/// response, so clients get one error shape no matter where the failure came
/// from.
///
/// Registered first in the pipeline, which means it also catches failures thrown
/// by the authentication, authorization and rate-limiting components below it.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. Nothing to report and nothing to fix.
            logger.LogDebug("Request {Path} was aborted by the client.", context.Request.Path);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, problemType, detail, errors) = Classify(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            // Unexpected: log with the stack trace, tell the client nothing.
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} (trace {TraceId}).",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }
        else
        {
            logger.LogInformation(
                "{StatusCode} for {Method} {Path}: {Title} (trace {TraceId}).",
                statusCode,
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }

        if (context.Response.HasStarted)
        {
            // Too late to change the status code or body; abort instead of
            // emitting a malformed response.
            logger.LogWarning(
                "Response already started for {Path}; cannot write problem details.",
                context.Request.Path);

            context.Abort();
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = problemType,
            Detail = detail,
            Instance = context.Request.Path,
        };

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        if (errors is { Count: > 0 })
        {
            problemDetails.Extensions["errors"] = errors;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        // Echoed as a header too, so a user can quote it when reporting a problem.
        context.Response.Headers["traceId"] = traceId;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    private static (int StatusCode, string Title, string Type, string Detail, IDictionary<string, string[]>? Errors)
        Classify(Exception exception)
    {
        var traceSuffix = " See response header 'traceId'.";

        switch (exception)
        {
            case ValidationFailedException validationFailed:
                return (
                    StatusCodes.Status400BadRequest,
                    "One or more validation errors occurred.",
                    "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    "The request was not valid.",
                    validationFailed.Errors);

            case ValidationException fluentValidation:
                return (
                    StatusCodes.Status400BadRequest,
                    "One or more validation errors occurred.",
                    "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    "The request was not valid.",
                    ToErrorDictionary(fluentValidation));

            case InvalidCredentialsException:
                // Same title and detail for a wrong password, an unknown email
                // and an inactive account: the response must not distinguish them.
                return (
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "https://tools.ietf.org/html/rfc6750#section-3.1",
                    "Invalid email or password.",
                    null);

            case AccountLockedException accountLocked:
                return (
                    StatusCodes.Status401Unauthorized,
                    "Account locked",
                    "https://tools.ietf.org/html/rfc6750#section-3.1",
                    accountLocked.Message + traceSuffix,
                    null);

            case InvalidRefreshTokenException invalidRefreshToken:
                return (
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "https://tools.ietf.org/html/rfc6750#section-3.1",
                    invalidRefreshToken.Message,
                    null);

            case ForbiddenOperationException forbidden:
                return (
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    forbidden.Message,
                    null);

            case EntityNotFoundException:
                return (
                    StatusCodes.Status404NotFound,
                    "Resource not found",
                    "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                    "The requested resource does not exist.",
                    null);

            case ConflictException conflict:
                return (
                    StatusCodes.Status409Conflict,
                    "Conflict",
                    "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    conflict.Message,
                    null);

            case BadHttpRequestException badRequest:
                return (
                    StatusCodes.Status400BadRequest,
                    "Malformed request",
                    "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    badRequest.Message,
                    null);

            default:
                return (
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.",
                    "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    "The server encountered an unexpected condition." + traceSuffix,
                    null);
        }
    }

    private static IDictionary<string, string[]> ToErrorDictionary(ValidationException exception)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var failure in exception.Errors)
        {
            // Failures without a property name (e.g. a rule on the whole object)
            // are collected under a neutral key.
            var key = string.IsNullOrWhiteSpace(failure.PropertyName) ? "general" : failure.PropertyName;

            errors[key] = errors.TryGetValue(key, out var existing)
                ? [.. existing, failure.ErrorMessage]
                : [failure.ErrorMessage];
        }

        return errors;
    }
}
