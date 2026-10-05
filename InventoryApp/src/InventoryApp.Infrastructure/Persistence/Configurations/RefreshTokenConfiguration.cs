using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.UserId)
            .IsRequired()
            .HasMaxLength(450); // Matches Identity's key column width.

        // Base64url of 32 bytes is 43 characters; 100 leaves headroom.
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(token => token.RevokedReason)
            .HasMaxLength(50);

        // Lookup path for every refresh request, so it must be unique: two
        // different raw tokens must never collapse to the same stored row.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        // Reuse detection and logout both revoke "the whole family".
        builder.HasIndex(token => token.FamilyId)
            .HasDatabaseName("IX_RefreshTokens_FamilyId");

        // Backing store for "revoke every session belonging to this user".
        builder.HasIndex(token => token.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        builder.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
