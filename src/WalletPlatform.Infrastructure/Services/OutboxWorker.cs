using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Data.Entities;

namespace WalletPlatform.Infrastructure.Services;

public sealed class OutboxWorker(IServiceProvider sp, ILogger<OutboxWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
                var due = await db.OutboxMessages
                    .FromSqlRaw("SELECT * FROM outbox_messages WHERE \"ProcessedAt\" IS NULL AND \"NextAttemptAt\" <= now() AND (\"LockedUntil\" IS NULL OR \"LockedUntil\" < now()) ORDER BY \"CreatedAt\" LIMIT 50 FOR UPDATE SKIP LOCKED")
                    .ToListAsync(stoppingToken);
                foreach (var m in due)
                {
                    m.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(2);
                }
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) { log.LogError(ex, "outbox poll failed"); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
