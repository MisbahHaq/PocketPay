using Inventory.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.Property(token => token.Id)
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.RevokedReason)
            .HasMaxLength(64);

        builder.Property(token => token.ReplacedByTokenHash)
            .HasMaxLength(128);

        builder.Property(token => token.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.RevokedAt)
            .HasColumnType("timestamp with time zone");

        // The refresh endpoint looks a token up by hash on every single call, and it is
        // the first thing a returning user hits. This index is the hot path.
        // A unique index also makes a hash collision impossible rather than unlikely.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_token_hash");

        // Replay detection and logout revoke a whole family at once.
        builder.HasIndex(token => token.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        // Deactivation revokes every token a user holds.
        builder.HasIndex(token => token.UserId)
            .HasDatabaseName("ix_refresh_tokens_user_id");

        // Supports the periodic cleanup of long-expired rows, which would otherwise
        // accumulate forever and slow every index above down.
        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");
    }
}