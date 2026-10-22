using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Domain;

/// <summary>
/// Helpers for accounting semantics that depend only on the account type.
/// </summary>
public static class AccountTypeExtensions
{
    /// <summary>
    /// True when an increase in this account is recorded on the credit side.
    /// Liabilities and revenue are credit-normal; assets are debit-normal.
    /// </summary>
    public static bool IsCreditNormal(this AccountType accountType) =>
        accountType is AccountType.Liability or AccountType.Revenue;
}
