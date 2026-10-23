using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class SettlementFileConfiguration : IEntityTypeConfiguration<SettlementFile>
{
    public void Configure(EntityTypeBuilder<SettlementFile> b)
    {
        b.ToTable("settlement_files");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillerCode).IsRequired().HasMaxLength(64);
        b.Property(x => x.FileHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.RawContent).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.HasIndex(x => new { x.BillerCode, x.BusinessDate, x.FileHash }).IsUnique();
    }
}
