using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Infrastructure.Data.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class SettlementRowConfiguration : IEntityTypeConfiguration<SettlementRow>
{
    public void Configure(EntityTypeBuilder<SettlementRow> b)
    {
        b.ToTable("settlement_rows");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillerReference).IsRequired().HasMaxLength(128);
        b.Property(x => x.ClientReference).IsRequired().HasMaxLength(128);
        b.Property(x => x.ConsumerNumber).IsRequired().HasMaxLength(64);
        b.Property(x => x.Currency).IsRequired().HasMaxLength(8);
        b.Property(x => x.Status).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.SettlementFileId);
    }
}
