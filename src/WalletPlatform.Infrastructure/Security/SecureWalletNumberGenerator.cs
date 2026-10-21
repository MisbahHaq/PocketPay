using System.Security.Cryptography;
using WalletPlatform.Application.Abstractions.Security;

namespace WalletPlatform.Infrastructure.Security;

internal sealed class SecureWalletNumberGenerator : IWalletNumberGenerator
{
    private const int Length = 12;

    public string Generate()
    {
        Span<char> digits = stackalloc char[Length];

        // The leading digit is never zero so the number always has 12 significant digits.
        digits[0] = (char)('1' + RandomNumberGenerator.GetInt32(9));
        for (var index = 1; index < digits.Length; index++)
        {
            digits[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }
}
