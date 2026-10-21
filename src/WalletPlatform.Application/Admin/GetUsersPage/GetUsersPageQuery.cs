namespace WalletPlatform.Application.Admin.GetUsersPage;

public sealed record GetUsersPageQuery(int Page = 1, int PageSize = 20);

public sealed record UserSummary(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset CreatedAtUtc,
    bool IsLockedOut);
