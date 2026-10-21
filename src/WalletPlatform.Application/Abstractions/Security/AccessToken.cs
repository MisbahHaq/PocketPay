namespace WalletPlatform.Application.Abstractions.Security;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc)
{
    public override string ToString() => $"AccessToken {{ ExpiresAtUtc = {ExpiresAtUtc:O}, Value = *** }}";
}
