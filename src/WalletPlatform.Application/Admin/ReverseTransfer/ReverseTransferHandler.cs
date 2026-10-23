using FluentValidation;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Abstractions.Services;
using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Admin.ReverseTransfer;

public sealed record ReverseTransferRequest(string? Description);

public sealed class ReverseTransferRequestValidator : AbstractValidator<ReverseTransferRequest>
{
    public ReverseTransferRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

/// <summary>
/// Reverses a completed transfer (admin only). This posts a reversal journal entry
/// and marks the transfer as Reversed. Idempotency is guaranteed by the unique index
/// on JournalEntry.ReversesJournalEntryId.
/// </summary>
public sealed class ReverseTransferHandler(
    ITransferRepository transfers,
    ILedgerService ledgerService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<TransferDetail> HandleAsync(
        Guid transferId,
        ReverseTransferRequest request,
        string reversedBy,
        CancellationToken cancellationToken)
    {
        var transfer = await transfers.GetByIdAsync(transferId, cancellationToken)
            ?? throw new NotFoundException("Transfer not found.");

        if (transfer.Status != TransferStatus.Completed)
        {
            throw new ConflictException("Only completed transfers can be reversed.");
        }

        if (transfer.JournalEntryId is null)
        {
            throw new NotFoundException("Transfer has no associated journal entry.");
        }

        // Reverse the journal entry
        var reversal = await ledgerService.ReverseAsync(
            transfer.JournalEntryId.Value,
            reversedBy,
            clock.UtcNow,
            request.Description ?? $"Admin reversal of transfer {transferId}");

        // Mark transfer as reversed
        transfer.MarkReversed();

        // Save in the same transaction (ReverseAsync already uses unitOfWork internally)
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetail(transfer);
    }

    private static TransferDetail MapToDetail(Transfer t) =>
        new(
            t.Id,
            t.SourceWalletId,
            t.DestinationWalletId,
            t.Amount.MinorUnits,
            t.Fee.MinorUnits,
            t.Amount.Currency,
            t.Status,
            t.Note,
            t.CreatedAt,
            t.CompletedAt,
            t.FailureReason,
            t.JournalEntryId);
}