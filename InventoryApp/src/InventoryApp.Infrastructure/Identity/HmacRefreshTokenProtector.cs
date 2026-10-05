using System.Security.Cryptography;
using System.Text;
using InventoryApp.Application.Common.Interfaces;

namespace InventoryApp.Infrastructure.Identity;

/// <summary>
/// Key used to hash refresh tokens. Separate from the access-token signing key
/// so the two can be rotated independently.
/// </summary>
public sealed class RefreshTokenProtectorOptions : IValidatableOptions
{
    public const string SectionName = "RefreshTokens:Protector";

    public const int MinimumHashingKeyLengthInBytes = 32;

    public string HashingKey { get; set; } = string.Empty;

    public IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(HashingKey)
            ? [$"{SectionName}:HashingKey is required. Set it with: "
               + "dotnet user-secrets set \"RefreshTokens:Protector:HashingKey\" \"<random-64-characters>\"."]
            : Encoding.UTF8.GetByteCount(HashingKey) < MinimumHashingKeyLengthInBytes
                ? [$"{SectionName}:HashingKey must be at least {MinimumHashingKeyLengthInBytes} bytes long."]
                : [];
}

/// <inheritdoc />
public sealed class HmacRefreshTokenProtector : IRefreshTokenProtector
{
    private const int TokenSizeInBytes = 32; // 256 bits of entropy

    private readonly byte[] _hashingKey;

    public HmacRefreshTokenProtector(byte[] hashingKey) => _hashingKey = hashingKey;

    public string Generate() => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeInBytes));

    public string Hash(string rawToken) =>
        Base64UrlEncode(HMACSHA256.HashData(_hashingKey, Encoding.UTF8.GetBytes(rawToken)));

    /// <summary>
    /// URL-safe encoding so the token can travel in a JSON body or a header
    /// without escaping, and so the stored hash has no '+' or '/' characters.
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
