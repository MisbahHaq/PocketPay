using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Services;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.CreateTransfer;

/// <summary>
/// Creates a wallet-to-wallet transfer. The source wallet must belong to the requesting
/// user (enforced by the handler looking up the wallet and checking UserId). The destination
/// is looked up by wallet number. A failed transfer is still persisted for audit.
/// </summary>
public sealed class CreateTransferHandler(
    IWalletRepository wallets,
    ITransferService transferService)
{
    public async Task<TransferSummary> HandleAsync(
        Guid requestingUserId,
        CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        // Verify source wallet belongs to the requesting user
        var sourceWallet = await wallets.FindByIdAsync(request.SourceWalletId, cancellationToken);

        if (sourceWallet is null || sourceWallet.UserId != requestingUserId)
        {
            throw new NotFoundException("Source wallet not found.");
        }

        var amount = new Money(request.AmountMinorUnits, request.Currency);
        var transfer = await transferService.ExecuteAsync(
            request.SourceWalletId,
            request.DestinationWalletNumber,
            amount,
            request.Note,
            cancellationToken);

        return MapToSummary(transfer);
    }

    private static TransferSummary MapToSummary(Transfer transfer) =>
        new(
            transfer.Id,
            transfer.SourceWalletId,
            transfer.DestinationWalletId,
            transfer.Amount.MinorUnits,
            transfer.Fee.MinorUnits,
            transfer.Amount.Currency,
            transfer.Status,
            transfer.Note,
            transfer.CreatedAt,
            transfer.CompletedAt,
            transfer.FailureReason);
}