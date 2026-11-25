namespace WalletPlatform.Application.Reconciliation;

public interface IReconciliationService
{
    /// <summary>Runs matching for a biller/business date against the latest imported settlement file.</summary>
    Task<Guid> RunAsync(string billerCode, DateOnly businessDate, CancellationToken ct = default);

    /// <summary>Parses and stores a settlement CSV file. Returns the settlement file id.</summary>
    Task<Guid> ImportSettlementFileAsync(
        string billerCode,
        DateOnly businessDate,
        string csvContent,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReconciliationItemDto>> GetItemsAsync(Guid runId, CancellationToken ct = default);
}

public sealed record ReconciliationItemDto(
    Guid Id,
    Guid RunId,
    string? ClientReference,
    Guid? PaymentId,
    string Category,
    long OurAmount,
    long BillerAmount,
    string? OurStatus,
    string? BillerStatus,
    string Resolution);
