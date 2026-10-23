namespace WalletPlatform.Infrastructure.Data.Entities;

public sealed class SettlementRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SettlementFileId { get; set; }
    public string BillerReference { get; set; } = default!;
    public string ClientReference { get; set; } = default!;
    public string ConsumerNumber { get; set; } = default!;
    public long AmountMinorUnits { get; set; }
    public string Currency { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTimeOffset ProcessedAt { get; set; }
}
