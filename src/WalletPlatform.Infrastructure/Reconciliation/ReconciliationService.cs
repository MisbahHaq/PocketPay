using WalletPlatform.Application.Reconciliation;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Reconciliation;

public sealed class ReconciliationService(WalletDbContext db) : IReconciliationService
{
    private readonly WalletDbContext _db = db;
    public async Task<Guid> RunAsync(string billerCode, DateOnly businessDate, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        await Task.CompletedTask;
        return runId;
    }
}
