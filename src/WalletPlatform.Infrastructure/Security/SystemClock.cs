using WalletPlatform.Application.Abstractions.Security;

namespace WalletPlatform.Infrastructure.Security;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
