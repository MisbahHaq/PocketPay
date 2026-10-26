using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(128);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(64);
        b.Property(x => x.EntityId).IsRequired().HasMaxLength(128);
        b.Property(x => x.Outcome).IsRequired().HasMaxLength(16);
        b.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Hash).IsRequired().HasMaxLength(128);
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
    }
}
