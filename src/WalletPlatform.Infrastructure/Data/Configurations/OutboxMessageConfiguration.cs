using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Infrastructure.Data.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).IsRequired().HasMaxLength(128);
        b.Property(x => x.Payload).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.LastError).HasMaxLength(2048);
        b.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
    }
}
