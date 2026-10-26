namespace WalletPlatform.Application.Audit;

public interface IAuditService
{
    Task RecordAsync(string action, string entityType, string entityId, string outcome, object? details = null, CancellationToken ct = default);
}
