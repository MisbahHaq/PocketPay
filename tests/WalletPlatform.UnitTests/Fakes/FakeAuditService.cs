using WalletPlatform.Application.Audit;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeAuditService : IAuditService
{
    public List<(string Action, string EntityType, string EntityId, string Outcome)> Records { get; } = [];

    public Task RecordAsync(
        string action,
        string entityType,
        string entityId,
        string outcome,
        object? details = null,
        CancellationToken ct = default)
    {
        Records.Add((action, entityType, entityId, outcome));
        return Task.CompletedTask;
    }
}
