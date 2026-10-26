namespace WalletPlatform.Application.Reconciliation;

public sealed class AgingPolicy(int maxAgeDays)
{
    public int MaxAgeDays { get; } = maxAgeDays;
    public bool IsEscalated(DateTimeOffset firstSeenUtc, DateTimeOffset nowUtc)
        => (nowUtc - firstSeenUtc).TotalDays >= MaxAgeDays;
}
