namespace WalletPlatform.Application.Auth.RegisterUser;

public sealed record RegisterUserResponse(Guid UserId, string Email, Guid WalletId, string WalletNumber);
