using System.Security.Cryptography;
using System.Text;

namespace InventoryApp.Application.Security;

/// <summary>
/// Generates refresh tokens and their stored hashes.
/// </summary>
public static class RefreshTokenGenerator
{
    /// <summary>512 bits of entropy, so the token is not guessable even without hashing.</summary>
    private const int TokenSizeInBytes = 64;

    /// <summary>Returns the plaintext token to hand to the client and the hash to persist.</summary>
    public static (string Value, string Hash) Create()
    {
        var value = ToBase64Url(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        return (value, Hash(value));
    }

    /// <summary>
    /// Plain SHA-256, not a password hash. The input is 512 bits of CSPRNG output with no
    /// structure to attack, so a slow KDF such as PBKDF2 would only add latency. This is safe
    /// precisely because the token is high-entropy; a user-chosen password would need the opposite.
    /// </summary>
    public static string Hash(string token) =>
        ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
