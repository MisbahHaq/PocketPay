using FluentValidation;
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
public sealed class GetAdminTransfersHandler
{
    public async Task<TransfersPageResponse> HandleAsync(
        GetAdminTransfersQuery query,
        CancellationToken cancellationToken)
    {
        // Placeholder: in a real implementation, we'd query all transfers with filters
        // For now, return empty - would need ITransferRepository.GetAllAsync
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        return new TransfersPageResponse([], page, pageSize, 0);
    }
}