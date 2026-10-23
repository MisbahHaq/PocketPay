using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords", table =>
        {
            table.HasCheckConstraint("CK_IdempotencyRecords_Key_Length", "\"Key\" ~ '^.{16,64}$'");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Key).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Method).IsRequired().HasMaxLength(10);
        builder.Property(r => r.Path).IsRequired().HasMaxLength(200);
        builder.Property(r => r.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(r => r.ResponseStatusCode);
        builder.Property(r => r.ResponseBody).HasMaxLength(10000);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();

        // Unique index on (UserId, Key) - keys are scoped per user
        builder.HasIndex(r => new { r.UserId, r.Key }).IsUnique();

        // Index for cleanup job
        builder.HasIndex(r => r.ExpiresAt);

        // Index for lookup by user
        builder.HasIndex(r => r.UserId);
    }
}