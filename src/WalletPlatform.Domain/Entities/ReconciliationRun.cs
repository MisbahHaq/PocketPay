namespace WalletPlatform.Domain.Entities;

public enum ReconciliationRunStatus { Running = 0, Completed = 1, Failed = 2 }
public enum ReconciliationTrigger { Scheduled = 0, Manual = 1 }

public sealed class ReconciliationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BillerCode { get; set; } = default!;
    public DateOnly BusinessDate { get; set; }
    public Guid? SettlementFileId { get; set; }
    public ReconciliationRunStatus Status { get; set; } = ReconciliationRunStatus.Running;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int MatchedCount { get; set; }
    public int MissingAtBillerCount { get; set; }
    public int UnknownAtOurSideCount { get; set; }
    public int AmountMismatchCount { get; set; }
    public Guid? SupersedesRunId { get; set; }
    public ReconciliationTrigger Trigger { get; set; }
    public Guid? SettlementJournalEntryId { get; set; }
}
