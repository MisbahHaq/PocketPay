namespace WalletPlatform.Application.Users.GetCurrentUser;

public sealed record WalletSummaryResponse(Guid Id, string WalletNumber, string Currency);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string Role,
    WalletSummaryResponse? Wallet);
