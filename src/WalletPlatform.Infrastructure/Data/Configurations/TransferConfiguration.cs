using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers", table =>
        {
            table.HasCheckConstraint(
                "CK_Transfers_Status",
                "\"Status\" IN ('Pending', 'Completed', 'Failed', 'Reversed')");

            // Amounts are stored as positive minor-unit magnitudes, like ledger postings.
            table.HasCheckConstraint("CK_Transfers_Amount_Positive", "\"AmountMinorUnits\" > 0");
            table.HasCheckConstraint("CK_Transfers_Fee_NonNegative", "\"FeeMinorUnits\" >= 0");

            // A transfer can never point at itself.
            table.HasCheckConstraint(
                "CK_Transfers_Distinct_Wallets",
                "\"SourceWalletId\" <> \"DestinationWalletId\"");

            // A completed transfer must reference its journal entry; a failed one must not.
            table.HasCheckConstraint(
                "CK_Transfers_Completed_Requires_JournalEntry",
                "(\"Status\" IN ('Completed', 'Reversed') AND \"JournalEntryId\" IS NOT NULL) " +
                "OR (\"Status\" IN ('Pending', 'Failed') AND \"JournalEntryId\" IS NULL)");
        });

        builder.HasKey(transfer => transfer.Id);

        builder.Property(transfer => transfer.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(transfer => transfer.Note).HasMaxLength(Transfer.MaxNoteLength);
        builder.Property(transfer => transfer.FailureReason).HasMaxLength(200);
        builder.Property(transfer => transfer.CreatedAt).IsRequired();

        // Money is two columns (amount + fee) rather than a stored Money object, so the
        // check constraints above can be expressed in SQL. The domain maps them back into
        // Money via the backing fields.
        builder.OwnsOne(transfer => transfer.Amount, money =>
        {
            money.Property(value => value.MinorUnits).HasColumnName("AmountMinorUnits").IsRequired();
            money.Property(value => value.Currency).HasColumnName("Currency")
                .HasConversion<string>().HasMaxLength(3).IsRequired();
        });
        builder.Navigation(transfer => transfer.Amount).IsRequired();

        builder.OwnsOne(transfer => transfer.Fee, money =>
        {
            money.Property(value => value.MinorUnits).HasColumnName("FeeMinorUnits").IsRequired();

            // The domain guarantees amount and fee share a currency, so the fee reuses the
            // single Currency column rather than duplicating it.
            money.Property(value => value.Currency).HasColumnName("Currency")
                .HasConversion<string>().HasMaxLength(3).IsRequired();
        });
        builder.Navigation(transfer => transfer.Fee).IsRequired();

        // "Transfers out of this wallet, newest first" — the history and daily-limit query.
        builder.HasIndex(transfer => new { transfer.SourceWalletId, transfer.CreatedAt });

        // "Transfers into this wallet, newest first" — the counterparty history query.
        builder.HasIndex(transfer => new { transfer.DestinationWalletId, transfer.CreatedAt });

        // One journal entry backs exactly one transfer; this stops a single entry being
        // linked to two transfers.
        builder.HasIndex(transfer => transfer.JournalEntryId)
            .IsUnique()
            .HasFilter("\"JournalEntryId\" IS NOT NULL");

        // Wallet references are not cascade-deleted; financial records outlive their wallets.
        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationWalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
