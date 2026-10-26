using WalletPlatform.Application.Audit;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Audit;

public sealed class AuditService(WalletDbContext db) : IAuditService
{
    public async Task RecordAsync(string action, string entityType, string entityId, string outcome, object? details = null, CancellationToken ct = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Outcome = outcome,
            Details = details is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(details)
        });
        await db.SaveChangesAsync(ct);
    }
}
