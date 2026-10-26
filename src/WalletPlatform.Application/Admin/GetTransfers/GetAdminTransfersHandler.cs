using FluentValidation;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Application.Admin.GetTransfers;

public sealed record GetAdminTransfersQuery(
    TransferStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20);

public sealed class GetAdminTransfersQueryValidator : AbstractValidator<GetAdminTransfersQuery>
{
    public GetAdminTransfersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>
/// Returns all transfers (admin only), paged and filterable by status and date range.
/// </summary>
public sealed class GetAdminTransfersHandler(ITransferRepository transfers)
{
    public async Task<TransfersPageResponse> HandleAsync(
        GetAdminTransfersQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var skip = (page - 1) * pageSize;

        var (items, totalCount) = await transfers.GetAllAsync(
            query.Status,
            query.From,
            query.To,
            skip,
            pageSize,
            cancellationToken);

        var mapped = items.Select(t => new TransferSummary(
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
            t.FailureReason)).ToList();

        return new TransfersPageResponse(mapped, page, pageSize, totalCount);
    }
}