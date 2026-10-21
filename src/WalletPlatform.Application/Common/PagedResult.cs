namespace WalletPlatform.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
}
