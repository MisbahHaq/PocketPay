using System.Security.Cryptography;
using System.Text;

namespace WalletPlatform.Infrastructure.Audit;

public static class AuditHasher
{
    public static string Hash(string previousHash, string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previousHash + content))).ToLowerInvariant();
    }
}
