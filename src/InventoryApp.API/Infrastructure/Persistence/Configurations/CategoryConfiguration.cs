using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for the category taxonomy. One level of nesting, so the FK is nullable and optional.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();

        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(500);
        builder.Property(category => category.IsActive).HasDefaultValue(true);

        // A unique name keeps a category unambiguous in a picker, and gives the duplicate check in
        // the service a database-level guarantee to fall back on.
        builder.HasIndex(category => category.Name).IsUnique();

        // Covers the self-join used to fetch one level of children, which is how the tree is read.
        builder.HasIndex(category => category.ParentCategoryId);

        builder.HasOne(category => category.ParentCategory)
            .WithMany(parent => parent.Children)
            .HasForeignKey(category => category.ParentCategoryId)
            // Restrict, not Cascade: deleting a parent with children would otherwise silently take
            // the children with it. The service refuses that case up front; this is the backstop.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(category => category.Products)
            .WithOne(product => product.Category)
            .HasForeignKey(product => product.CategoryId)
            // Same reasoning from the other side: a product must never disappear because the category
            // it is filed under went away.
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(category => category.CanHaveChildren);
    }
}