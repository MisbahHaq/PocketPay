using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            // Invariant 3: amounts are strictly positive. The direction carries the sign.
            table.HasCheckConstraint("CK_LedgerEntries_Amount_Positive", "\"AmountMinorUnits\" > 0");

            table.HasCheckConstraint(
                "CK_LedgerEntries_Direction",
                "\"Direction\" IN ('Debit', 'Credit')");
        });

        builder.HasKey(posting => posting.Id);

        builder.Property(posting => posting.Direction).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(posting => posting.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(posting => posting.AmountMinorUnits).IsRequired();
        builder.Property(posting => posting.CreatedAtUtc).IsRequired();

        // Statement query: postings for one account, newest first.
        builder.HasIndex(posting => new { posting.LedgerAccountId, posting.CreatedAtUtc });

        // Grouping postings by their journal entry (balance checks, reversals).
        builder.HasIndex(posting => posting.JournalEntryId);

        builder.HasOne<LedgerAccount>()
            .WithMany()
            .HasForeignKey(posting => posting.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JournalEntry>()
            .WithMany(entry => entry.Postings)
            .HasForeignKey(posting => posting.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
