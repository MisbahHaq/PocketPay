using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);

        // SHA-256 rendered as lowercase hex is always 64 characters.
        builder.Property(token => token.TokenHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Reuse detection revokes every token in a family, so lookups are by family.
        builder.HasIndex(token => token.FamilyId);

        builder.Property(token => token.CreatedAtUtc).IsRequired();
        builder.Property(token => token.ExpiresAtUtc).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
