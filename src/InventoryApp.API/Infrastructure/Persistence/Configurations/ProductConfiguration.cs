using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for the product catalogue row.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();

        builder.Property(product => product.Sku).HasMaxLength(30).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(1000);

        builder.Property(product => product.UnitOfMeasure).HasConversion<int>().IsRequired();
        builder.Property(product => product.ReorderLevel).HasDefaultValue(0);

        // decimal(18,2) rather than the default decimal(18,2)-with-extra-scale, and never float or
        // double: binary floating point cannot represent 0.10 exactly, so a price would drift a
        // fraction of a cent on every stored value and the totals would not add up. Two decimal
        // places is two more than any realistic unit price needs and leaves room for currencies with
        // minor units smaller than a cent.
        builder.Property(product => product.CostPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(product => product.SalePrice).HasPrecision(18, 2).IsRequired();

        builder.Property(product => product.IsActive).HasDefaultValue(true);
        builder.Property(product => product.IsDeleted).HasDefaultValue(false);

        // Identity user id, so it matches the length of the column it points at.
        builder.Property(product => product.CreatedBy).HasMaxLength(450);
        builder.Property(product => product.UpdatedBy).HasMaxLength(450);
        builder.Property(product => product.DeletedBy).HasMaxLength(450);

        builder.HasIndex(product => product.Sku).IsUnique();

        // The catalogue's main access path: a page of products filtered by category, or by
        // "still in the catalogue at all". IsDeleted is deliberately in the index so the global
        // filter can use it rather than scanning.
        builder.HasIndex(product => new { product.CategoryId, product.IsDeleted });

        builder.HasIndex(product => new { product.IsDeleted, product.IsActive });

        // Sortable set for ?sortBy=createdAt.
        builder.HasIndex(product => new { product.CreatedAt });

        // Sortable set for ?sortBy=name, which is the default order of the list endpoint.
        builder.HasIndex(product => new { product.Name });

        builder.HasOne(product => product.Category)
            .WithMany(category => category.Products)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optimistic concurrency token. EF appends the original value to the WHERE clause of any
        // update or delete, so two clients that read the same row cannot both write: the second one
        // matches no rows and is reported as a conflict.
        //
        // Deliberately client-generated rather than SQL Server's native `rowversion`. A store-
        // generated token cannot be produced by SQLite, which the integration suite runs on, so
        // using it would mean two code paths with the tests only covering the one that never ships.
        // A fresh Guid per write behaves identically under optimistic concurrency - the original
        // value is still what gets compared - and one code path is genuinely covered end to end.
        // See ProductService for how a mismatch surfaces to the caller.
        builder.Property(product => product.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        // The global soft-delete filter. Set here, beside the rest of the mapping, rather than in
        // OnModelCreating so OnModelCreating stays free of logic.
        //
        // Defaulting to hidden is the point: forgetting to check IsDeleted is then a safe mistake
        // (a product stays invisible) instead of an unsafe one (a deleted product sells). Anything
        // that legitimately needs the deleted rows - restore, cascade checks, reporting - states so
        // explicitly with IgnoreQueryFilters(), which makes those places easy to find by grepping.
        builder.HasQueryFilter(product => !product.IsDeleted);
    }
}