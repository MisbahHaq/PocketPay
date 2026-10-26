using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Audit;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Audit;

public sealed class AuditService(WalletDbContext db) : IAuditService
{
    public async Task RecordAsync(string action, string entityType, string entityId, string outcome, object? details = null, CancellationToken ct = default)
    {
        var detailsJson = details is null ? "{}" : JsonSerializer.Serialize(details);

        var last = await db.AuditEvents
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);

        var previousHash = last?.Hash ?? string.Empty.PadRight(64, '0');
        var content = $"{DateTimeOffset.UtcNow:O}|{action}|{entityType}|{entityId}|{outcome}|{detailsJson}|{previousHash}";
        var hash = AuditHasher.Hash(previousHash, content);

        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Outcome = outcome,
            Details = detailsJson,
            PreviousHash = previousHash,
            Hash = hash
        });
        await db.SaveChangesAsync(ct);
    }
}
