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

        builder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}