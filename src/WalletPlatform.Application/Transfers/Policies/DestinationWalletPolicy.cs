using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.Policies;

/// <summary>
/// 2. Destination wallet must exist and be looked up by 12-digit wallet number.
/// 3. Source and destination must differ.
/// </summary>
public sealed class DestinationWalletPolicy
{
    public PolicyResult Check(Guid sourceWalletId, Guid? destinationWalletId, string? destinationWalletNumber)
    {
        if (destinationWalletId is null)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.DestinationNotFound,
                $"No wallet found with number {destinationWalletNumber}.");
        }

        if (sourceWalletId == destinationWalletId)
        {
            return PolicyResult.Fail(
                TransferErrorCodes.SameWallet,
                "Source and destination wallets must be different.");
        }

        return PolicyResult.Success();
    }
}