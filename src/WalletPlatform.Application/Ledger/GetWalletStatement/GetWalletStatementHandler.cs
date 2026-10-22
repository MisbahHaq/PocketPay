using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Ledger;

namespace WalletPlatform.Application.Ledger.GetWalletStatement;

/// <summary>
/// Returns a page of statement lines for a wallet that belongs to the requesting user.
/// As with the balance endpoint, another user's wallet is reported as 404.
/// </summary>
public sealed class GetWalletStatementHandler(
    IWalletRepository wallets,
    ILedgerAccountRepository ledgerAccounts,
    ILedgerService ledger)
{
    public async Task<StatementResponse> HandleAsync(
        GetWalletStatementQuery query,
        CancellationToken cancellationToken)
    {
        var wallet = await wallets.FindByIdAsync(query.WalletId, cancellationToken);

        if (wallet is null || wallet.UserId != query.RequestingUserId)
        {
            throw new NotFoundException("Wallet not found.");
        }

        var account = await ledgerAccounts.FindByWalletIdAsync(wallet.Id, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        var statement = await ledger.GetStatementAsync(
            account.Id, query.Page, query.PageSize, cancellationToken);

        var lines = statement.Lines
            .Select(line => new StatementLine(
                line.JournalEntryId,
                line.CreatedAtUtc,
                line.Direction.ToString(),
                line.AmountMinorUnits,
                line.RunningBalanceMinorUnits,
                line.Type,
                line.Reference,
                line.Description))
            .ToList();

        return new StatementResponse(
            statement.LedgerAccountId,
            statement.WalletId,
            statement.Currency.ToString(),
            statement.ClosingBalanceMinorUnits,
            statement.Page,
            statement.PageSize,
            statement.TotalCount,
            lines);
    }
}
