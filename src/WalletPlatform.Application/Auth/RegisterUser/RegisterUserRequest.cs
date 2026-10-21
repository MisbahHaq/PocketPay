namespace WalletPlatform.Application.Auth.RegisterUser;

public sealed record RegisterUserRequest(string Email, string Password)
{
    public override string ToString() => $"RegisterUserRequest {{ Email = {Email}, Password = *** }}";
}
