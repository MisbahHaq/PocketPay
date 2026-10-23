namespace WalletPlatform.Infrastructure.Services;

public sealed class BillPaymentOutcomeHandler
{
    public Task HandleSuccessAsync(Guid paymentId, string billerReference, CancellationToken ct) => Task.CompletedTask;
    public Task HandleDeclineAsync(Guid paymentId, string reason, CancellationToken ct) => Task.CompletedTask;
    public Task HandleUnknownAsync(Guid paymentId, CancellationToken ct) => Task.CompletedTask;
}
