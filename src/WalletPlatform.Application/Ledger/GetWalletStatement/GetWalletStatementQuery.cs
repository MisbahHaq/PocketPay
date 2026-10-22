namespace WalletPlatform.Application.Ledger.GetWalletStatement;

public sealed record GetWalletStatementQuery(Guid WalletId, Guid RequestingUserId, int Page = 1, int PageSize = 20);
