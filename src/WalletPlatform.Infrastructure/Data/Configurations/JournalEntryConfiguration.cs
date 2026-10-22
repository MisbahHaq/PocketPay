using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data.Configurations;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Type).IsRequired().HasMaxLength(40);
        builder.Property(entry => entry.Reference).HasMaxLength(100);
        builder.Property(entry => entry.Description).HasMaxLength(500);
        builder.Property(entry => entry.CreatedBy).IsRequired().HasMaxLength(200);
        builder.Property(entry => entry.CreatedAtUtc).IsRequired();

        // Postings are part of the aggregate and are reached through the navigation.
        // The relationship itself is configured on the LedgerEntry side.
        builder.Metadata
            .FindNavigation(nameof(JournalEntry.Postings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // A reversal may point at its original entry, but only one reversal per original.
        builder.HasIndex(entry => entry.ReversesJournalEntryId)
            .IsUnique()
            .HasFilter("\"ReversesJournalEntryId\" IS NOT NULL");

        // Statement / trial-balance scans are ordered by time.
        builder.HasIndex(entry => entry.CreatedAtUtc);
    }
}
