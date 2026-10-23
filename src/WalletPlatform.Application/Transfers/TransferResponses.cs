using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers;

/// <summary>
/// Minimal display info for a wallet when looking up by number. No balance, no email.
/// </summary>
public sealed record WalletLookupResponse(
    Guid Id,
    string MaskedDisplayName,
    Currency Currency);

/// <summary>
/// Customer-facing transfer summary.
/// </summary>
public sealed record TransferSummary(
    Guid Id,
    Guid SourceWalletId,
    Guid DestinationWalletId,
    long AmountMinorUnits,
    long FeeMinorUnits,
    Currency Currency,
    TransferStatus Status,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);

/// <summary>
/// Detailed transfer view (owner or admin).
/// </summary>
public sealed record TransferDetail(
    Guid Id,
    Guid SourceWalletId,
    Guid DestinationWalletId,
    long AmountMinorUnits,
    long FeeMinorUnits,
    Currency Currency,
    TransferStatus Status,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    Guid? JournalEntryId);

/// <summary>
/// Paged list of transfers.
/// </summary>
public sealed record TransfersPageResponse(
    IReadOnlyList<TransferSummary> Items,
    int Page,
    int PageSize,
    int TotalCount);