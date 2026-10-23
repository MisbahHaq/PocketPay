using Microsoft.EntityFrameworkCore;

namespace WalletPlatform.MockBiller.Data;

public sealed class BillerTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BillerReference { get; set; } = default!;
    public string ClientReference { get; set; } = default!;
    public string BillerCode { get; set; } = default!;
    public string ConsumerNumber { get; set; } = default!;
    public long AmountMinorUnits { get; set; }
    public string Status { get; set; } = "Success";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MockBillerDbContext(DbContextOptions<MockBillerDbContext> options) : DbContext(options)
{
    public DbSet<BillerTransaction> Transactions { get; set; } = default!;
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<BillerTransaction>(e =>
        {
            e.ToTable("biller_transactions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ClientReference }).IsUnique();
        });
    }
}
