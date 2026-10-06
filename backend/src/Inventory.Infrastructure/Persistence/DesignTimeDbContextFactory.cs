using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Inventory.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> create a context without booting the whole API.
/// </summary>
/// <remarks>
/// Needed because the real <c>Program</c> composes Serilog, MediatR, rate limiting and
/// CORS, none of which a design-time tool should have to satisfy. The connection string
/// is read from user secrets or the environment exactly as at runtime; it is only needed
/// to pick a provider and to infer a database name in <c>migrations add</c>, so the EF
/// tooling works even before a database exists.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=inventory_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new InventoryDbContext(options);
    }
}