using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");
        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.WalletNumber).IsRequired().HasMaxLength(12);
        builder.HasIndex(wallet => wallet.WalletNumber).IsUnique();

        builder.Property(wallet => wallet.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();

        // Financial records are never cascade-deleted with their owner.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(wallet => wallet.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
