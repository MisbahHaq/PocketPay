namespace WalletPlatform.Domain.Enums;

/// <summary>
/// Accounting classification of a <see cref="Entities.LedgerAccount"/>.
///
/// In double-entry accounting every account is either debit-normal or credit-normal,
/// which tells us how to read its balance:
///   - Asset and Revenue accounts are DEBIT-normal  (balance = debits - credits).
///   - Liability accounts are CREDIT-normal         (balance = credits - debits).
///
/// Customer wallets are <see cref="Liability"/> because the money in them belongs to
/// the customer, not the platform. From the platform's perspective a wallet balance is
/// something the platform OWES, i.e. a liability, so it is credit-normal.
/// </summary>
public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Revenue = 2,
}
