using InventoryApp.API.Extensions;
using InventoryApp.API.Middleware;
using InventoryApp.Application;
using InventoryApp.Infrastructure;
using InventoryApp.Infrastructure.Persistence;
using InventoryApp.Infrastructure.Seeding;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryApp.API;

/// <summary>
/// The composition root: builds the web application and its pipeline.
///
/// It is a separate class from <c>Program.cs</c> on purpose - the integration
/// tests call <see cref="CreateApp"/> directly, so the tests exercise the exact
/// same registration and middleware order that production runs, with no test-only
/// copy of the wiring to keep in sync.
/// </summary>
public static class InventoryAppHost
{
    /// <param name="args">Process arguments, forwarded to the configuration builder.</param>
    /// <param name="configureBuilder">
    /// Invoked after the host is created but before any layer is registered, so
    /// the integration tests can supply in-memory configuration (test signing
    /// keys, a SQLite connection string, an isolated rate limit) without any
    /// service registration being patched afterwards.
    /// </param>
    public static WebApplication CreateApp(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        configureBuilder?.Invoke(builder);

        builder.Services.AddApplicationLayer();
        builder.Services.AddInfrastructureLayer(builder.Configuration);
        builder.Services.AddApiLayer(builder.Configuration);

        // Lets the integration tests replace the DbContext registration with a
        // throwaway SQLite database. No-op in production.
        builder.Services.RemoveAll<IHostLifetime>();

        var app = builder.Build();

        ConfigurePipeline(app);

        return app;
    }

    /// <summary>
    /// Applies migrations and seeds roles/administrator. Kept out of the pipeline
    /// so it runs exactly once, before the first request, and so a test can call
    /// it deliberately.
    /// </summary>
    public static async Task InitializeDatabaseAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        // First in the pipeline so it also converts failures coming out of the
        // authentication, rate limiting and authorization components below it.
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Management API v1");
                options.DocumentTitle = "Inventory Management API";
            });
        }

        app.UseRouting();

        // Authentication before the rate limiter: the /refresh limiter
        // partitions on the authenticated user id.
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapControllers();

        // A minimal liveness endpoint, also useful as the smoke-test target.
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
