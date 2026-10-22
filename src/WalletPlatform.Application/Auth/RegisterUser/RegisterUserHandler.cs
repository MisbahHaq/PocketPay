using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Application.Auth.RegisterUser;

public sealed class RegisterUserHandler(
    IUserRepository users,
    IWalletRepository wallets,
    ILedgerAccountRepository ledgerAccounts,
    IWalletNumberGenerator walletNumberGenerator,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<RegisterUserResponse> HandleAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);

        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var now = clock.UtcNow;
        var user = User.Create(email, passwordHasher.Hash(request.Password), Role.Customer, now);

        // The user row, wallet row and the wallet's ledger account are written in one
        // transaction. If any step fails, everything rolls back so we never persist a
        // wallet without a ledger account.
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            users.Add(user);
            await unitOfWork.SaveChangesAsync(token);

            var wallet = Wallet.Create(user.Id, walletNumberGenerator.Generate(), Currency.PKR, now);
            wallets.Add(wallet);
            await unitOfWork.SaveChangesAsync(token);

            ledgerAccounts.Add(LedgerAccount.CreateForWallet(wallet.Id, wallet.Currency, now));
            await unitOfWork.SaveChangesAsync(token);

            return new RegisterUserResponse(user.Id, user.Email, wallet.Id, wallet.WalletNumber);
        }, cancellationToken);
    }
}
