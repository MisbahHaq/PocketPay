using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common;

namespace WalletPlatform.Application.Admin.GetUsersPage;

public sealed class GetUsersPageHandler(IUserRepository users, IClock clock)
{
    public async Task<PagedResult<UserSummary>> HandleAsync(
        GetUsersPageQuery query,
        CancellationToken cancellationToken)
    {
        var page = await users.GetPageAsync(query.Page, query.PageSize, cancellationToken);
        var now = clock.UtcNow;

        var items = page.Items
            .Select(user => new UserSummary(
                user.Id,
                user.Email,
                user.Role.ToString(),
                user.CreatedAtUtc,
                user.IsLockedOut(now)))
            .ToList();

        return new PagedResult<UserSummary>(items, page.TotalCount, page.Page, page.PageSize);
    }
}
