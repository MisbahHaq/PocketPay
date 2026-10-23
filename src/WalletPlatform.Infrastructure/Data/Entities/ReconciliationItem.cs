namespace WalletPlatform.Infrastructure.Data.Entities;

public sealed class ReconciliationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public string? ClientReference { get; set; }
    public Guid? PaymentId { get; set; }
    public string Category { get; set; } = default!;
    public long OurAmount { get; set; }
    public long BillerAmount { get; set; }
    public string? OurStatus { get; set; }
    public string? BillerStatus { get; set; }
    public string Resolution { get; set; } = "Open";
    public string? ResolutionNote { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
