using InventoryApp.Application.Abstractions;
using InventoryApp.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// Unit of work over the whole application. As each module lands it adds its own DbSet here, so
/// modules 2+ share one schema and can be joined without cross-database queries.
/// </summary>
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IdentityDbContext<ApplicationUser>(options), IUnitOfWork
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's table configuration is applied first so our configurations extend it
        // instead of replacing it.
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    /// <summary>
    /// Stamps the audit columns before every write, so no service has to remember to and no caller
    /// can forge one.
    /// </summary>
    /// <remarks>
    /// Done here rather than in an interceptor because it needs the request-scoped
    /// <see cref="ICurrentUserService"/>, and a context is already constructed once per request, so
    /// the dependency is free. The alternative - a <c>SaveChangesInterceptor</c> - would have to
    /// reach the same scoped service through the change tracker anyway.
    /// </remarks>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    void IUnitOfWork.ExpectRowVersion(Product product, byte[] rowVersion) =>
        Entry(product).Property(existing => existing.RowVersion).OriginalValue = rowVersion;

    /// <summary>
    /// Fills CreatedBy/UpdatedBy/UpdatedAt for tracked products. Only products carry audit columns in
    /// this module; a wider surface is a one-line change to the type list.
    /// </summary>
    private void ApplyAuditInformation()
    {
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    // Set on insert too, so "last updated" is never null on a row nobody has edited.
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    entry.Entity.RowVersion = NextRowVersion();
                    break;

                case EntityState.Modified:
                    // Set explicitly rather than left to the database, so an unchanged value is
                    // still reported as "touched" and the change set is honest.
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    // Rotating the token on every write is what makes the next stale client fail:
                    // it sends the value it read, which no longer matches the row.
                    entry.Entity.RowVersion = NextRowVersion();
                    break;
            }
        }
    }

    /// <summary>
    /// Opaque, unique-per-write concurrency token. Its content is never interpreted, so a Guid is
    /// as good as anything and costs nothing to compare in the WHERE clause.
    /// </summary>
    private static byte[] NextRowVersion() => Guid.NewGuid().ToByteArray();
}