using Microsoft.Extensions.Hosting;

namespace WalletPlatform.Infrastructure.Reconciliation;

public sealed class ReconciliationScheduler : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
