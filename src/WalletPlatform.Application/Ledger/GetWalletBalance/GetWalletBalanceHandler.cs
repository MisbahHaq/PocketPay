using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Ledger;

namespace WalletPlatform.Application.Ledger.GetWalletBalance;

/// <summary>
/// Returns the balance of a wallet that belongs to the requesting user. A wallet owned by
/// somebody else is reported as 404 (not 403) so the endpoint never reveals whether a
/// wallet id exists.
/// </summary>
public sealed class GetWalletBalanceHandler(
    IWalletRepository wallets,
    ILedgerAccountRepository ledgerAccounts,
    ILedgerService ledger)
{
    public async Task<BalanceResponse> HandleAsync(
        GetWalletBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var wallet = await wallets.FindByIdAsync(query.WalletId, cancellationToken);

        if (wallet is null || wallet.UserId != query.RequestingUserId)
        {
            throw new NotFoundException("Wallet not found.");
        }

        var account = await ledgerAccounts.FindByWalletIdAsync(wallet.Id, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        var balance = await ledger.GetBalanceAsync(account.Id, cancellationToken) ?? 0;

        return new BalanceResponse(
            account.Id,
            account.WalletId,
            account.Currency.ToString(),
            account.Type.ToString(),
            balance);
    }
}
