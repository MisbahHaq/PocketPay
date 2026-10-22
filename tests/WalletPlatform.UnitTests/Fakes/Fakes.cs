using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
}

internal sealed class FakeTokenService : ITokenService
{
    public int RefreshTokenLengthInChars => 64;

    public AccessToken CreateAccessToken(User user, DateTimeOffset nowUtc) =>
        new($"access-{user.Id}", nowUtc.AddMinutes(15));

    public RefreshTokenValue CreateRefreshToken(DateTimeOffset nowUtc)
    {
        var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        return new RefreshTokenValue(raw, HashRefreshToken(raw), nowUtc.AddDays(7));
    }

    public string HashRefreshToken(string rawToken) => $"hash::{rawToken}";
}

internal sealed class FakeWalletNumberGenerator(string walletNumber) : IWalletNumberGenerator
{
    private int _calls;

    public int Calls => _calls;

    public string Generate()
    {
        _calls++;
        return walletNumber;
    }
}
