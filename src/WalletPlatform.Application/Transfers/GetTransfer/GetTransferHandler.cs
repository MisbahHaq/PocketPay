using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.GetTransfer;

/// <summary>
/// Returns a single transfer by id. The requester must own either the source or
/// destination wallet, or be an admin (admin check is at the policy level).
/// Returns 404 if the transfer doesn't exist or the user doesn't own it.
/// </summary>
public sealed class GetTransferHandler(
    IWalletRepository wallets,
    ITransferRepository transfers)
{
    public async Task<TransferDetail> HandleAsync(
        Guid requestingUserId,
        Guid transferId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);

        if (transfer is null)
        {
            throw new NotFoundException("Transfer not found.");
        }

        // Non-admins must own either the source or destination wallet
        if (!isAdmin)
        {
            var sourceWallet = await wallets.FindByIdAsync(transfer.SourceWalletId, cancellationToken);
            var destWallet = await wallets.FindByIdAsync(transfer.DestinationWalletId, cancellationToken);

            var ownsSource = sourceWallet?.UserId == requestingUserId;
            var ownsDest = destWallet?.UserId == requestingUserId;

            if (!ownsSource && !ownsDest)
            {
                throw new NotFoundException("Transfer not found.");
            }
        }

        return MapToDetail(transfer);
    }

    private static TransferDetail MapToDetail(Transfer t) =>
        new(
            t.Id,
            t.SourceWalletId,
            t.DestinationWalletId,
            t.Amount.MinorUnits,
            t.Fee.MinorUnits,
            t.Amount.Currency,
            t.Status,
            t.Note,
            t.CreatedAt,
            t.CompletedAt,
            t.FailureReason,
            t.JournalEntryId);
}