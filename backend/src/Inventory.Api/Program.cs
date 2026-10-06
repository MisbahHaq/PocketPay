using Inventory.Api.Extensions;
using Inventory.Infrastructure;
using Serilog;
using Serilog.Events;

// ---------------------------------------------------------------------------
// Bootstrap logger.
//
// Created before the host exists so that a failure while reading configuration - a
// malformed connection string, an invalid JWT key - is still reported through a
// sink. Configuring Serilog inside the builder would leave exactly those startup
// errors invisible, which is when you most need them.
// ---------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Hand Serilog the built IConfiguration, so its sinks, levels and enrichers can be
    // configured from appsettings.json, user secrets and environment variables.
    builder.Host.UseSerilog((context, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);

    var app = builder.Build();

    await app.ApplyDatabaseMigrationsAsync();
    app.ConfigureApiPipeline();

    Log.Information(
        "Inventory API starting. Environment={Environment} Version={Version}",
        app.Environment.EnvironmentName,
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Inventory API terminated unexpectedly");

    // Rethrow so the process exits non-zero. Without this a failed start would look
    // like a clean shutdown to Docker or the orchestrator, and a restart loop would
    // hide the real cause.
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the real pipeline in
/// integration tests. Top-level statements generate an internal Program class, which
/// the test host cannot see.
/// </summary>
public partial class Program
{
    protected Program()
    {
    }
}