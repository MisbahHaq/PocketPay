using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;

namespace WalletPlatform.Application.Users.GetCurrentUser;

public sealed class GetCurrentUserHandler(IUserRepository users, IWalletRepository wallets)
{
    public async Task<CurrentUserResponse> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(query.UserId, cancellationToken)
            ?? throw new UnauthorizedException("The authenticated user no longer exists.");

        var wallet = await wallets.FindByUserIdAsync(user.Id, cancellationToken);

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.Role.ToString(),
            wallet is null
                ? null
                : new WalletSummaryResponse(wallet.Id, wallet.WalletNumber, wallet.Currency.ToString()));
    }
}
