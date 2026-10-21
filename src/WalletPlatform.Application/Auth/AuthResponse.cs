namespace WalletPlatform.Application.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public override string ToString() =>
        $"AuthResponse {{ AccessToken = ***, RefreshToken = ***, " +
        $"AccessTokenExpiresAtUtc = {AccessTokenExpiresAtUtc:O}, " +
        $"RefreshTokenExpiresAtUtc = {RefreshTokenExpiresAtUtc:O} }}";
}
