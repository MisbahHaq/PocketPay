using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;

namespace WalletPlatform.Domain.Entities;

/// <summary>
/// An account in the double-entry ledger. Every customer wallet has exactly one
/// <see cref="LedgerAccount"/>, and the platform has a small number of system accounts
/// (funding, fees, biller settlement).
///
/// A ledger account has NO stored balance. Its balance is derived from its postings
/// (see <see cref="LedgerEntry"/>) because a cached balance can drift from the truth
/// and can never be rebuilt.
/// </summary>
public sealed class LedgerAccount
{
    private LedgerAccount()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Set for customer accounts: the wallet this account backs.</summary>
    public Guid? WalletId { get; private set; }

    /// <summary>Set for system accounts: which well-known account this is.</summary>
    public SystemAccount? SystemCode { get; private set; }

    public string Name { get; private set; } = null!;
    public AccountType Type { get; private set; }
    public Currency Currency { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsCreditNormal => Type.IsCreditNormal();

    /// <summary>
    /// Creates the ledger account that backs a customer wallet. Customer wallets are
    /// LIABILITY accounts: the balance represents money the platform owes the customer.
    /// </summary>
    public static LedgerAccount CreateForWallet(Guid walletId, Currency currency, DateTimeOffset createdAtUtc)
    {
        if (walletId == Guid.Empty)
        {
            throw new LedgerException("Wallet id is required for a wallet ledger account.");
        }

        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            SystemCode = null,
            Name = $"Customer wallet {walletId}",
            Type = AccountType.Liability,
            Currency = currency,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>
    /// Creates one of the platform's system accounts. The account type is fixed per code
    /// so it cannot be misconfigured.
    /// </summary>
    public static LedgerAccount CreateSystem(
        SystemAccount systemCode,
        Currency currency,
        DateTimeOffset createdAtUtc)
    {
        var (name, type) = systemCode switch
        {
            SystemAccount.Funding => ("System funding", AccountType.Asset),
            SystemAccount.Fees => ("System fees", AccountType.Revenue),
            SystemAccount.BillerSettlement => ("System biller settlement", AccountType.Liability),
            _ => throw new LedgerException($"Unknown system account code '{systemCode}'."),
        };

        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            WalletId = null,
            SystemCode = systemCode,
            Name = name,
            Type = type,
            Currency = currency,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
