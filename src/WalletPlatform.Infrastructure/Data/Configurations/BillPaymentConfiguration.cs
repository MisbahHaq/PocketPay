using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Infrastructure.Data.Configurations;

public sealed class BillPaymentConfiguration : IEntityTypeConfiguration<BillPayment>
{
    public void Configure(EntityTypeBuilder<BillPayment> b)
    {
        b.ToTable("bill_payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillerCode).IsRequired().HasMaxLength(64);
        b.Property(x => x.ConsumerNumber).IsRequired().HasMaxLength(64);
        b.Property(x => x.FailureReason).HasMaxLength(256);
        b.Property(x => x.BillerReference).HasMaxLength(128);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Amount).HasConversion(v => v.MinorUnits, v => new Money(v, Currency.PKR))
            .HasColumnName("AmountMinorUnits");
        b.HasIndex(x => x.WalletId);
        b.HasIndex(x => x.RefundJournalEntryId).IsUnique().HasFilter("\"RefundJournalEntryId\" IS NOT NULL");
    }
}
