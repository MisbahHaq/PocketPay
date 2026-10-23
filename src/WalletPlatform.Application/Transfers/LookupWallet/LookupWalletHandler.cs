using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Transfers.LookupWallet;

/// <summary>
/// Looks up a wallet by its 12-digit wallet number. Returns only a masked display name
/// (derived from the owner's email) and currency — never email, balance, or owner identity.
/// Returns 404 if not found.
/// </summary>
public sealed class LookupWalletHandler(
    IWalletRepository wallets,
    IUserRepository users)
{
    public async Task<WalletLookupResponse> HandleAsync(
        string walletNumber,
        CancellationToken cancellationToken)
    {
        var wallet = await wallets.FindByWalletNumberAsync(walletNumber, cancellationToken);

        if (wallet is null)
        {
            throw new NotFoundException("Wallet not found.");
        }

        var owner = await users.FindByIdAsync(wallet.UserId, cancellationToken);
        var masked = MaskEmail(owner?.Email ?? string.Empty);

        return new WalletLookupResponse(wallet.Id, masked, wallet.Currency);
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return "***";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return new string('*', email.Length);

        var localPart = email[..atIndex];
        var domain = email[atIndex..];

        var maskedLocal = localPart.Length <= 2
            ? new string('*', localPart.Length)
            : $"{localPart[0]}***{localPart[^1]}";

        return maskedLocal + domain;
    }
}