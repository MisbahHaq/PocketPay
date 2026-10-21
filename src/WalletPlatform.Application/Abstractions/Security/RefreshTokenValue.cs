namespace WalletPlatform.Application.Abstractions.Security;

public sealed record RefreshTokenValue(string RawValue, string TokenHash, DateTimeOffset ExpiresAtUtc)
{
    public override string ToString() =>
        $"RefreshTokenValue {{ ExpiresAtUtc = {ExpiresAtUtc:O}, RawValue = ***, TokenHash = *** }}";
}
