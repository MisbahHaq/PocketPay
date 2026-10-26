namespace WalletPlatform.Application.Options;

public sealed class RateLimitOptions
{
    public int AuthPerMinute { get; set; } = 10;
    public int MoneyPerMinute { get; set; } = 60;
    public int ReadPerMinute { get; set; } = 300;
}
