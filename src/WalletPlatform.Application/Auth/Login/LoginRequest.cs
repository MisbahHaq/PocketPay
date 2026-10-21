namespace WalletPlatform.Application.Auth.Login;

public sealed record LoginRequest(string Email, string Password)
{
    public override string ToString() => $"LoginRequest {{ Email = {Email}, Password = *** }}";
}
