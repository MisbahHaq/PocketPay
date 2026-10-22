namespace WalletPlatform.Domain.Enums;

/// <summary>
/// The lifecycle of a transfer. A transfer is created <see cref="Pending"/> and moves to
/// exactly one terminal state. The only legal transitions are:
///
///   Pending  -> Completed   (the journal entry posted and the money moved)
///   Pending  -> Failed      (a business rule rejected the request)
///   Completed -> Reversed   (an admin corrected a completed transfer)
///
/// Anything else (e.g. Failed -> anything, or Reversed -> anything) is illegal and is
/// rejected by the domain. The status is stored, but it never carries money: the balances
/// themselves still come only from ledger postings.
/// </summary>
public enum TransferStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Reversed = 3,
}
