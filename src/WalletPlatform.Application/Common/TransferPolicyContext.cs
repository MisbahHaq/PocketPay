using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Common;

/// <summary>
/// The resolved, policy-ready inputs for a transfer. The <see cref="TransferService"/>
/// builds this once (auth + wallet lookup + fee computation) and hands it to each rule;
/// every rule then sees already-resolved domain objects rather than raw strings and
/// Guids, which keeps the rules small and unit-testable.
/// </summary>
public sealed class TransferPolicyContext(
    Guid callerId,
    Wallet sourceWallet,
    Wallet? destinationWallet,
    Money amount,
    Money fee,
    string? note,
    IClock clock,
    IWalletRepository wallets,
    ILedgerAccountRepository ledgerAccounts,
    ITransferRepository transfers,
    ILedgerService ledger,
    long sourceBalanceMinorUnits,
    long completedOutgoingMinorUnits,
    long dailyOutgoingLimitMinorUnits)
{
    public Guid CallerId { get; } = callerId;
    public Wallet SourceWallet { get; } = sourceWallet;
    public Wallet? DestinationWallet { get; } = destinationWallet;
    public Money Amount { get; } = amount;
    public Money Fee { get; } = fee;
    public string? Note { get; } = note;
    public IClock Clock { get; } = clock;
    public IWalletRepository Wallets { get; } = wallets;
    public ILedgerAccountRepository LedgerAccounts { get; } = ledgerAccounts;
    public ITransferRepository Transfers { get; } = transfers;
    public ILedgerService Ledger { get; } = ledger;

    /// <summary>
    /// The source wallet's derived balance, in minor units, resolved by the service
    /// before the rules run. Positive means the platform owes the customer money.
    /// </summary>
    public long SourceBalanceMinorUnits { get; } = sourceBalanceMinorUnits;

    /// <summary>
    /// Total amount + fee of completed transfers out of the source wallet in the rolling
    /// 24-hour window ending now, in minor units. Resolved by the service before the
    /// rules run so the limit rule stays a pure comparison.
    /// </summary>
    public long CompletedOutgoingMinorUnits { get; } = completedOutgoingMinorUnits;

    /// <summary>The configured per-wallet rolling 24-hour outgoing cap, in minor units.</summary>
    public long DailyOutgoingLimitMinorUnits { get; } = dailyOutgoingLimitMinorUnits;

    /// <summary>Total the source wallet must cover: amount plus fee.</summary>
    public Money TotalDebit => Amount + Fee;
}

/// <summary>
/// Outcome of one transfer rule. A failure carries a stable machine-readable code (the
/// <c>code</c> extension in the ProblemDetails response) and a human message.
/// </summary>
public sealed record TransferPolicyResult(bool IsSuccess, string? FailureCode, string? FailureReason)
{
    public static TransferPolicyResult Success() => new(true, null, null);

    public static TransferPolicyResult Fail(string code, string reason) => new(false, code, reason);
}

/// <summary>
/// One transfer business rule. Each rule is its own class so it can be unit-tested in
/// isolation with a fake clock and a fake ledger, rather than being hidden inside one
/// giant <c>ExecuteAsync</c> method.
/// </summary>
public interface ITransferPolicy
{
    Task<TransferPolicyResult> EvaluateAsync(TransferPolicyContext context, CancellationToken cancellationToken);
}