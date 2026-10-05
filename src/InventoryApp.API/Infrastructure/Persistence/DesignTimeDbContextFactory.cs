using InventoryApp.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// Builds a context for <c>dotnet ef</c> without starting the application.
/// </summary>
/// <remarks>
/// Without this, the EF tools have to boot <c>Program.cs</c> to find the context, which means every
/// migration command inherits whatever the startup path does - seeding, background services, and a
/// real connection string. This factory keeps migrations a pure schema operation, and it is also why
/// the tooling works with no appsettings overrides in place.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // The schema to scaffold is a property of the migration, not of the environment, so the
        // connection string here is only used to pick the provider's SQL dialect and never to open a
        // connection. EF does not connect while generating a migration.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=InventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options, new DesignTimeUser(), TimeProvider.System);
    }

    /// <summary>
    /// Migrations never touch a user, and the audit columns are nullable precisely so a system
    /// write needs no identity. This stands in for the request-scoped HTTP-context implementation.
    /// </summary>
    private sealed class DesignTimeUser : ICurrentUserService
    {
        public bool IsAuthenticated => false;

        public string? UserId => null;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => [];
    }
}