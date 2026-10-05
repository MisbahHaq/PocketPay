using InventoryApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryApp.Tests.Support;

/// <summary>
/// Boots the real Program.cs with two substitutions: SQLite instead of SQL Server, and known
/// secrets instead of user-secrets. Everything else - DI wiring, Identity, JWT validation,
/// rate limiting, the exception handler - is the production composition.
/// </summary>
public sealed class InventoryApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Str0ng!AdminPassw0rd";
    public const string SigningKey = "integration-test-signing-key-of-at-least-32-chars";

    private readonly SqliteConnection _connection;

    public InventoryApiFactory()
    {
        // The database lives in this connection, so it must stay open for the factory's lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironments.Name);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused-in-tests",
                ["Jwt:Issuer"] = "InventoryApp.Api",
                ["Jwt:Audience"] = "InventoryApp.Clients",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["SeedAdmin:Email"] = AdminEmail,
                ["SeedAdmin:Password"] = AdminPassword,

                // Raised well above the number of requests these tests make, including the
                // deliberate wrong-password ones, so the limiter never masks the behaviour under test.
                ["RateLimiting:LoginPermitLimit"] = "10000",
                ["RateLimiting:RefreshPermitLimit"] = "10000"
            }));

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers with TryAdd semantics, so the SQL Server registrations have to
            // be removed before the SQLite ones will take effect.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}