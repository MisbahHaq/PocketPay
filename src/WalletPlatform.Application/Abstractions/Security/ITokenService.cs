using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Security;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, DateTimeOffset nowUtc);

    RefreshTokenValue CreateRefreshToken(DateTimeOffset nowUtc);

    string HashRefreshToken(string rawToken);
}
