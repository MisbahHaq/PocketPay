using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.GetTransfers;

/// <summary>
/// Returns paginated transfers for the requesting user's wallet. The user can only see
/// transfers where they own either the source or destination wallet. Direction filter
/// ("outgoing" | "incoming") narrows to transfers from or to the wallet.
/// </summary>
public sealed class GetTransfersHandler(
    IWalletRepository wallets,
    ITransferRepository transfers)
{
    public async Task<TransfersPageResponse> HandleAsync(
        Guid requestingUserId,
        GetTransfersQuery query,
        CancellationToken cancellationToken)
    {
        // Verify the wallet belongs to the user
        if (query.WalletId is not null)
        {
            var wallet = await wallets.FindByIdAsync(query.WalletId.Value, cancellationToken);

            if (wallet is null || wallet.UserId != requestingUserId)
            {
                throw new NotFoundException("Wallet not found.");
            }
        }

        var walletId = query.WalletId
            ?? throw new InvalidOperationException("WalletId is required.");

        var from = query.From ?? DateTimeOffset.MinValue;
        var to = query.To ?? DateTimeOffset.MaxValue;
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var skip = (page - 1) * pageSize;

        IReadOnlyList<Transfer> items;
        long totalCount;

        if (query.Direction == "incoming")
        {
            items = await transfers.GetByDestinationWalletIdAsync(walletId, from, to, skip, pageSize, cancellationToken);
            totalCount = await transfers.GetByDestinationWalletIdCountAsync(walletId, from, to, cancellationToken);
        }
        else // outgoing or null
        {
            items = await transfers.GetBySourceWalletIdAsync(walletId, from, to, skip, pageSize, cancellationToken);
            totalCount = await transfers.GetBySourceWalletIdCountAsync(walletId, from, to, cancellationToken);
        }

        var paged = items.Select(MapToSummary).ToList();

        return new TransfersPageResponse(paged, page, pageSize, (int)totalCount);
    }

    private static TransferSummary MapToSummary(Transfer t) =>
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
            t.FailureReason);
}