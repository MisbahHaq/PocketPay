namespace WalletPlatform.Infrastructure.Data.Entities;

public sealed class PaymentReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = "Unreconciled";
    public Guid? RunId { get; set; }
}
