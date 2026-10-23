using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> b)
    {
        b.ToTable("reconciliation_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillerCode).IsRequired().HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Trigger).HasConversion<int>();
        b.HasIndex(x => new { x.BillerCode, x.BusinessDate });
        b.HasIndex(x => x.SettlementJournalEntryId).IsUnique().HasFilter("\"SettlementJournalEntryId\" IS NOT NULL");
    }
}
