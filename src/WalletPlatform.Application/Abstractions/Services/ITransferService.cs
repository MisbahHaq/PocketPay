using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Abstractions.Services;

/// <summary>
/// Orchestrates a wallet-to-wallet transfer. The implementation must ensure the transfer
/// and its journal entry are created in a single transaction.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Executes a transfer from the caller's wallet to the destination wallet identified
    /// by wallet number. Returns the created transfer (which will be Completed on success,
    /// or Failed with a reason if a business rule rejects it).
    /// </summary>
    /// <exception cref="NotFoundException">Source wallet not found for the caller, or destination wallet number not found.</exception>
    /// <exception cref="UnauthorizedException">The caller does not own the source wallet.</exception>
    /// <exception cref="TransferException">A domain invariant was violated (source == destination, currency mismatch, etc.).</exception>
    Task<Transfer> ExecuteAsync(
        Guid sourceWalletId,
        string destinationWalletNumber,
        Money amount,
        string? note,
        CancellationToken cancellationToken = default);
}