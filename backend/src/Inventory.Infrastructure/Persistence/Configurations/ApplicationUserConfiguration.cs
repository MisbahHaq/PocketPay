using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.FullName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(user => user.LastLoginAt)
            .HasColumnType("timestamp with time zone");

        // IHasRowVersion -> PostgreSQL's xmin system column. The database supplies and
        // compares it, so two writers cannot both win on the same row.
        builder.Property(user => user.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xmin")
            .IsRowVersion();

        // Identity's own indexes cover UserName and NormalizedUserName. Email is
        // different: FindByEmailAsync filters on NormalizedEmail, which is looked up on
        // every single login. Without this index, sign-in degrades to a sequential scan
        // once the user table grows.
        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("ix_users_normalized_email");

        // Deactivated accounts are listed in the admin screen filtered on this flag.
        builder.HasIndex(user => user.IsActive)
            .HasDatabaseName("ix_users_is_active");

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}