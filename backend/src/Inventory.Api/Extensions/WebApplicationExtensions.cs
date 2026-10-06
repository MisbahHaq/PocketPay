using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using Serilog;
using Serilog.Context;
using Inventory.Api.Middleware;
using Inventory.Infrastructure.Persistence.Seeding;

namespace Inventory.Api.Extensions;

/// <summary>Host pipeline construction.</summary>
public static class WebApplicationExtensions
{
    public static WebApplication ConfigureApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Forwarded headers must be read before HTTPS redirection and rate limiting, both
        // of which otherwise see the proxy's address rather than the client's.
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

            // Bound to one hop. Trusting every hop lets a client prepend a forged
            // X-Forwarded-For entry and choose its own rate-limit bucket.
            ForwardLimit = 1,
        });

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, timeout, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= 500)
                {
                    return Serilog.Events.LogEventLevel.Error;
                }

                // Health checks are polled constantly by orchestrators; at Information
                // level they would bury real traffic.
                if (httpContext.Request.Path.StartsWithSegments("/health"))
                {
                    return Serilog.Events.LogEventLevel.Verbose;
                }

                return httpContext.Response.StatusCode >= 400
                    ? Serilog.Events.LogEventLevel.Warning
                    : Serilog.Events.LogEventLevel.Information;
            };
        });

        // Serilog's enricher so every log line carries the request's trace id, which is
        // also what the error responses return to the client.
        app.Use(async (context, next) =>
        {
            LogContext.PushProperty("TraceId", Activity.Current?.Id ?? context.TraceIdentifier);
            await next();
        });

        // First in the pipeline: it must wrap everything that could throw, including
        // routing and model binding that happen further down.
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseSecurityHeaders();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseResponseCompression();

        app.UseCors("spa");

        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (var description in app.Services
                             .GetRequiredService<Asp.Versioning.ApiExplorer.IApiVersionDescriptionProvider>()
                             .ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        description.GroupName.ToUpperInvariant());
                }
            });
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapHealthChecks("/health/live", new()
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponse,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse,
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Applies database migrations and seeds roles on startup.
    /// </summary>
    /// <remarks>
    /// Off by default outside Development, because running migrations from every
    /// application instance is unsafe: N instances starting at once will race to apply the
    /// same migration. In production this should be one deliberate job, which is what
    /// <c>Database:ApplyMigrationsOnStartup</c> being false by default protects against.
    /// </remarks>
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
        {
            return;
        }

await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        try
        {
            await seeder.MigrateAsync(cancellationToken);
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            // Without this, the console gets a 30-line Npgsql stack dump that says nothing
            // about what to fix. This is the first failure a fresh checkout hits, and the
            // useful part is one sentence: which setting is wrong.
            throw new InvalidOperationException(
                "Could not prepare the database. Check ConnectionStrings:DefaultConnection "
                + "(dotnet user-secrets list) and that PostgreSQL is running with the "
                + "database and role created. See README.md step 1.",
                exception);
        }
    }

    /// <summary>
    /// Minimal hardening headers.
    /// </summary>
    /// <remarks>
    /// Scoped to what applies to a JSON API. A full Content-Security-Policy is aimed at
    /// browsers rendering HTML; here the concern is content sniffing and framing, and a
    /// strict policy would only risk breaking Swagger.
    /// </remarks>
    private static void UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            // Authorization must never be cached by an intermediary or the browser.
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                headers["Pragma"] = "no-cache";
            }

            await next();
        });
    }

    private static Task WriteHealthResponse(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                error = entry.Value.Exception?.Message,
            }),
        };

return context.Response.WriteAsync(JsonSerializer.Serialize(payload, HealthJsonOptions));
    }

    private static readonly JsonSerializerOptions HealthJsonOptions = new(JsonSerializerDefaults.Web);
}