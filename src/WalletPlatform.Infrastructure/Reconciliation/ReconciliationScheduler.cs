using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WalletPlatform.Application.Reconciliation;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Reconciliation;

/// <summary>
/// Periodically reconciles the previous business day for every enabled biller that has
/// an imported settlement file. Failures are logged per biller so one bad file does not
/// stop the others.
/// </summary>
public sealed class ReconciliationScheduler(
    IServiceProvider services,
    ILogger<ReconciliationScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reconciliation scheduler started");
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcilePendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reconciliation sweep failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReconcilePendingAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IReconciliationService>();

        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5).AddDays(-1));

        var candidates = await (
            from file in db.SettlementFiles.AsNoTracking()
            where file.BusinessDate == businessDate && file.Status == SettlementFileStatus.Imported
            select new { file.BillerCode }
            ).Distinct().ToListAsync(ct);

        var alreadyRun = await db.ReconciliationRuns
            .AsNoTracking()
            .Where(r => r.BusinessDate == businessDate)
            .Select(r => r.BillerCode)
            .ToListAsync(ct);

        foreach (var candidate in candidates.Where(c => !alreadyRun.Contains(c.BillerCode)))
        {
            try
            {
                await service.RunAsync(candidate.BillerCode, businessDate, ct);
                logger.LogInformation(
                    "Reconciled {BillerCode} for {BusinessDate:yyyy-MM-dd}",
                    candidate.BillerCode, businessDate);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Reconciliation failed for {BillerCode} on {BusinessDate:yyyy-MM-dd}",
                    candidate.BillerCode, businessDate);
            }
        }
    }
}
