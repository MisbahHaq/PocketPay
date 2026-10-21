namespace WalletPlatform.Application.Auth.Refresh;

public sealed record RefreshTokenRequest(string RefreshToken)
{
    public override string ToString() => "RefreshTokenRequest { RefreshToken = *** }";
}
