using Microsoft.EntityFrameworkCore;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Infrastructure.Data;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WalletDbContext).Assembly);
    }
}
