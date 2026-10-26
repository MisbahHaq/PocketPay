using System.Security.Cryptography;
using System.Text;

namespace WalletPlatform.Application.Reconciliation;

public static class ReconciliationLockKey
{
    public static long For(string billerCode, DateOnly businessDate)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{billerCode}:{businessDate:yyyy-MM-dd}"));
        return BitConverter.ToInt64(bytes, 0);
    }
}
