namespace WalletPlatform.Application.Ledger.GetWalletBalance;

public sealed record GetWalletBalanceQuery(Guid WalletId, Guid RequestingUserId);
