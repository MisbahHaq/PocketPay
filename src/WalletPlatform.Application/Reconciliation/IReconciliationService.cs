namespace WalletPlatform.Application.Reconciliation;

public interface IReconciliationService
{
    Task<Guid> RunAsync(string billerCode, DateOnly businessDate, CancellationToken ct = default);
}
