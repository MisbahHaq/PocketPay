namespace WalletPlatform.Application.Options;

public sealed class RateLimitOptions
{
    public int AuthPerMinute { get; set; } = 10;
    public int MoneyPerMinute { get; set; } = 60;
    public int ReadPerMinute { get; set; } = 300;
    public int AdminPerMinute { get; set; } = 120;
    public int GlobalPerMinute { get; set; } = 1000;
}
