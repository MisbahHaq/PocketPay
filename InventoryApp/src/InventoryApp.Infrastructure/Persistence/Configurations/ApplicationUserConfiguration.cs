using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(user => user.FullName)
            .HasMaxLength(200);

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Supports "list the active users in this warehouse" style queries
        // without scanning the whole table.
        builder.HasIndex(user => new { user.IsActive, user.CreatedAt })
            .HasDatabaseName("IX_Users_IsActive_CreatedAt");
    }
}
