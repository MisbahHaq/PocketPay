using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WalletPlatform.Infrastructure.Reconciliation;

public sealed class ReconciliationScheduler(ILogger<ReconciliationScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reconciliation scheduler started");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
