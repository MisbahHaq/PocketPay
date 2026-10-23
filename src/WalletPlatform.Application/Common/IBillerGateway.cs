namespace WalletPlatform.Application.Common;

public interface IBillerGateway
{
    Task<BillerResult> SubmitPaymentAsync(Guid paymentId, string billerCode, string consumerNumber, long amountMinorUnits, string correlationId, CancellationToken ct);
    Task<BillerResult> GetStatusAsync(Guid paymentId, CancellationToken ct);
    Task<(bool Valid, string? ConsumerName, long? AmountDueMinorUnits)> ValidateConsumerAsync(string billerCode, string consumerNumber, CancellationToken ct);
}
