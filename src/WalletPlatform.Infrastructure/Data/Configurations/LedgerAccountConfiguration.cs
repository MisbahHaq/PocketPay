using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("LedgerAccounts", table =>
        {
            // An account is either a wallet account or a system account, never both/neither.
            table.HasCheckConstraint(
                "CK_LedgerAccounts_Owner",
                "(\"WalletId\" IS NOT NULL AND \"SystemCode\" IS NULL) OR " +
                "(\"WalletId\" IS NULL AND \"SystemCode\" IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_LedgerAccounts_Type",
                "\"Type\" IN ('Asset', 'Liability', 'Revenue')");
        });

        builder.HasKey(account => account.Id);

        // Account type and currency are stored as readable strings so a human (and a
        // CHECK constraint) can reason about the raw rows without a lookup table.
        builder.Property(account => account.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(account => account.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(account => account.SystemCode).HasConversion<string>().HasMaxLength(30);
        builder.Property(account => account.Name).IsRequired().HasMaxLength(200);

        // One ledger account per wallet.
        builder.HasIndex(account => account.WalletId).IsUnique();

        // One account per system code (per currency).
        builder.HasIndex(account => new { account.SystemCode, account.Currency })
            .IsUnique()
            .HasFilter("\"SystemCode\" IS NOT NULL");

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(account => account.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
