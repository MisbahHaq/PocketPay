using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Audit;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Audit;

public sealed class AuditService(WalletDbContext db, IAuditContextAccessor contextAccessor) : IAuditService
{
    // A transaction-scoped advisory lock serializes hash-chain appends. Without it two
    // concurrent RecordAsync calls could read the same "last" event and fork the chain.
    private const long HashChainLockKey = 0x5059504F434B4554; // "PAYPOCKET"

    public async Task RecordAsync(
        string action,
        string entityType,
        string entityId,
        string outcome,
        object? details = null,
        CancellationToken ct = default)
    {
        var detailsJson = details is null ? "{}" : JsonSerializer.Serialize(details);
        var context = contextAccessor.Current;

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(@key)", new object[] { HashChainLockKey }, ct);

                var last = await db.AuditEvents
                    .OrderByDescending(e => e.OccurredAt)
                    .ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(ct);

                var occurredAt = DateTimeOffset.UtcNow;
                var previousHash = last?.Hash ?? string.Empty.PadRight(64, '0');
                var content = $"{occurredAt:O}|{action}|{entityType}|{entityId}|{outcome}|{detailsJson}|{previousHash}";
                var hash = AuditHasher.Hash(previousHash, content);

                db.AuditEvents.Add(new AuditEvent
                {
                    OccurredAt = occurredAt,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Outcome = outcome,
                    Details = detailsJson,
                    PreviousHash = previousHash,
                    Hash = hash,
                    ActorUserId = context.ActorUserId,
                    ActorRole = context.ActorRole,
                    CorrelationId = context.CorrelationId,
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}
