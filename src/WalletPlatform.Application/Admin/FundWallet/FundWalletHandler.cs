using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Admin.FundWallet;

public sealed record FundWalletResponse(Guid JournalEntryId, Guid WalletId, long AmountMinorUnits, string Currency);

/// <summary>
/// Simulates a deposit into a wallet: the platform funding account is debited and the
/// wallet's liability account is credited, as one balanced journal entry. This is an
/// admin-only operation for this week; real customer-initiated top-ups come later.
/// </summary>
public sealed class FundWalletHandler(
    IWalletRepository wallets,
    ILedgerAccountRepository ledgerAccounts,
    ILedgerService ledger,
    IClock clock)
{
    public async Task<FundWalletResponse> HandleAsync(
        Guid walletId,
        FundWalletRequest request,
        string fundedBy,
        CancellationToken cancellationToken)
    {
        var wallet = await wallets.FindByIdAsync(walletId, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        var walletAccount = await ledgerAccounts.FindByWalletIdAsync(wallet.Id, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        var fundingAccount = await ledgerAccounts.FindBySystemCodeAsync(
            SystemAccount.Funding, wallet.Currency, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The system funding account for {wallet.Currency} has not been seeded.");

        var amount = new Money(request.AmountMinorUnits, wallet.Currency);

        var entry = JournalEntry.Create(
                type: "Funding",
                createdBy: fundedBy,
                createdAtUtc: clock.UtcNow,
                description: $"Funding of wallet {wallet.WalletNumber}.")
            .Debit(fundingAccount, amount)
            .Credit(walletAccount, amount)
            .Build();

        await ledger.PostAsync(entry, cancellationToken);

        return new FundWalletResponse(entry.Id, wallet.Id, amount.MinorUnits, wallet.Currency.ToString());
    }
}
