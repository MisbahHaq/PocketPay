using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.Infrastructure.Errors;

/// <summary>
/// Writes the ProblemDetails shape used everywhere else in the API. Needed because the JwtBearer
/// challenge and forbid handlers do not go through the exception pipeline, and would otherwise
/// return bare status codes that are the only errors a client cannot parse.
/// </summary>
internal static class ProblemDetailsWriter
{
    public static Task WriteAsync(HttpContext httpContext, int statusCode, string title, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions = { ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier }
        };

        return httpContext.Response.WriteAsJsonAsync(problemDetails, httpContext.RequestAborted);
    }
}