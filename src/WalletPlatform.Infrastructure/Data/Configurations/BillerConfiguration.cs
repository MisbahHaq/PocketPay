using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class BillerConfiguration : IEntityTypeConfiguration<Biller>
{
    public void Configure(EntityTypeBuilder<Biller> b)
    {
        b.ToTable("billers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(64);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
        b.Property(x => x.ConsumerNumberRegex).IsRequired().HasMaxLength(256);
        b.HasIndex(x => x.Code).IsUnique();
    }
}
