using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Domain.Entities;

public sealed class Biller
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string ConsumerNumberRegex { get; set; } = default!;
    public bool Enabled { get; set; } = true;
}
