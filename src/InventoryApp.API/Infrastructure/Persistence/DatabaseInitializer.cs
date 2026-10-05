using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>Environment name used by the test host.</summary>
public static class TestingEnvironments
{
    public const string Name = "Testing";
}

public static class DatabaseInitializer
{
    /// <summary>
    /// Brings the schema up to date and seeds roles and the admin account. Runs once at startup
    /// so a developer only has to run the API.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        var environment = provider.GetRequiredService<IHostEnvironment>();

        // Integration tests run on SQLite, which cannot execute the SQL Server migration scripts, so
        // it builds its schema straight from the model instead.
        if (environment.IsEnvironment(TestingEnvironments.Name))
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        else
            await dbContext.Database.MigrateAsync(cancellationToken);

        await provider.GetRequiredService<DatabaseSeeder>().SeedAsync(cancellationToken);
    }
}