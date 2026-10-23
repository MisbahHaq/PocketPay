namespace WalletPlatform.Domain.Entities;

public enum SettlementFileStatus { Imported = 0, Rejected = 1 }

public sealed class SettlementFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BillerCode { get; set; } = default!;
    public DateOnly BusinessDate { get; set; }
    public string FileHash { get; set; } = default!;
    public string RawContent { get; set; } = default!;
    public int RowCount { get; set; }
    public long TotalAmount { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public SettlementFileStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SupersedesRunId { get; set; }
}
