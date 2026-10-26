using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WalletPlatform.Infrastructure.Reconciliation;

public sealed class ReconciliationScheduler(ILogger<ReconciliationScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Reconciliation scheduler tick");
            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
