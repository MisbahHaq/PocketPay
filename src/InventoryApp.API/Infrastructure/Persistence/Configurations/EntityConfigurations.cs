using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FullName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.IsActive).HasDefaultValue(true);

        // Navigation is owned by the RefreshToken side of the relationship.
        builder.Ignore(user => user.RefreshTokens);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.UserId).HasMaxLength(450).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(token => token.RevokedReason).HasMaxLength(64);

        // Lookup path for every authenticated request, and the uniqueness guarantee that one
        // token value maps to at most one row.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Supports reuse detection, which revokes a family in a single statement.
        builder.HasIndex(token => new { token.FamilyId, token.RevokedAt });

        builder.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(token => token.IsRevoked);
    }
}
