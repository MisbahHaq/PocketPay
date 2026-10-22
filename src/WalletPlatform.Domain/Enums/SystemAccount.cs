namespace WalletPlatform.Domain.Enums;

/// <summary>
/// Well-known platform accounts. Each has a stable code so seeding can be idempotent
/// (find-or-create by code) and so funding/settlement flows can look them up by meaning
/// rather than by a hard-coded GUID.
///
/// Customer wallets are NOT in this enum: they are created per user and identified by
/// their <c>WalletId</c>, not by a fixed code.
/// </summary>
public enum SystemAccount
{
    /// <summary>Where simulated deposits come from (an asset, debit-normal).</summary>
    Funding = 0,

    /// <summary>Where collected fees accrue (revenue, credit-normal).</summary>
    Fees = 1,

    /// <summary>Holds money owed to billers between payment and settlement (liability).</summary>
    BillerSettlement = 2,
}
