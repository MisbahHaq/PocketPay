using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Common;

/// <summary>
/// Request to execute a wallet-to-wallet transfer. Amounts are in minor units (paisa).
/// The service resolves the source wallet from the caller and the destination from the
/// wallet number, so the client only ever supplies wallet numbers and amounts.
/// </summary>
public sealed record TransferRequest(
    Guid SourceWalletId,
    string DestinationWalletNumber,
    long AmountMinorUnits,
    string Currency,
    string? Note);

/// <summary>
/// Response returned on a successful transfer (HTTP 201).
/// </summary>
public sealed record TransferResponse(
    Guid TransferId,
    Guid SourceWalletId,
    string SourceWalletNumber,
    Guid DestinationWalletId,
    string DestinationWalletNumber,
    long AmountMinorUnits,
    long FeeMinorUnits,
    string Currency,
    string Status,
    string? Note,
    Guid JournalEntryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset CompletedAt);

/// <summary>
/// Summary row for transfer history (both sent and received).
/// </summary>
public sealed record TransferSummary(
    Guid TransferId,
    Guid SourceWalletId,
    string SourceWalletNumber,
    Guid DestinationWalletId,
    string DestinationWalletNumber,
    long AmountMinorUnits,
    long FeeMinorUnits,
    string Currency,
    string Status,
    string? Note,
    DateTimeOffset CreatedAt,
    bool IsOutgoing);

/// <summary>
/// Detail view of a single transfer, including the journal entry that moved the money.
/// </summary>
public sealed record TransferDetail(
    Guid TransferId,
    Guid SourceWalletId,
    string SourceWalletNumber,
    Guid DestinationWalletId,
    string DestinationWalletNumber,
    long AmountMinorUnits,
    long FeeMinorUnits,
    string Currency,
    string Status,
    string? Note,
    Guid? JournalEntryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    bool IsOutgoing);

/// <summary>
/// Masked destination lookup result. Deliberately narrow: it exposes a masked display
/// name and the currency only, never the owner's email or balance (see DECISIONS).
/// </summary>
public sealed record WalletLookupResponse(string WalletNumber, string MaskedName, string Currency);

/// <summary>
/// Query for transfer history. Direction filters to outgoing only, incoming only, or both.
/// </summary>
public sealed record TransferHistoryQuery(
    int Page,
    int PageSize,
    string? Direction,
    string? Currency,
    DateTimeOffset? From,
    DateTimeOffset? To);