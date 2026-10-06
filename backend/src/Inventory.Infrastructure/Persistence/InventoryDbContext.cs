using Inventory.Application.Abstractions;
using Inventory.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>
/// The single database context: Identity tables and inventory tables in one model.
/// </summary>
/// <remarks>
/// <para>
/// One context, not two. Two would mean two connection strings, two migration
/// histories and a cross-context transaction if a stock write needed both - for no
/// gain. A single context also lets a stock movement and the acting user's identity
/// live in the same transaction, which the audit trail depends on.
/// </para>
/// <para>
/// It inherits from <c>IdentityDbContext</c> so ASP.NET Identity gets its seven tables
/// with the exact schema it expects. Re-implementing user storage by hand is a
/// security risk with no upside.
/// </para>
/// </remarks>
public class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must come first: the Identity base builds its own schema and relies on the
        // naming conventions already being in place when it configures its tables.
        base.OnModelCreating(builder);

        ApplySnakeCaseIdentityTables(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }

    /// <summary>
    /// Renames ASP.NET Identity's seven tables to snake_case.
    /// </summary>
    /// <remarks>
    /// The naming convention only applies to columns EF derives a name for. Identity
    /// hard-codes its own table names ("AspNetUsers"), so left alone the schema would be
    /// a mix of PascalCase and snake_case - legal, but inconsistent enough to be a
    /// standing source of quoting bugs in hand-written SQL and reports.
    /// </remarks>
    private static void ApplySnakeCaseIdentityTables(ModelBuilder builder)
    {
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}