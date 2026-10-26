using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WalletPlatform.Infrastructure.Reconciliation;

public sealed class ReconciliationHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy());
}
