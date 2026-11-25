using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Audit;
using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Persistence;

namespace WalletPlatform.Infrastructure.Services;

/// <summary>
/// Applies the biller's response to a submitted payment. A decline reverses the
/// reservation by posting a compensating journal entry back to the customer's wallet;
/// an unknown outcome leaves the payment in RequiresReview for an operator.
/// </summary>
public sealed class BillPaymentOutcomeHandler(
    WalletDbContext db,
    IAccountLocker locker,
    ILedgerService ledger,
    ILedgerAccountRepository ledgerAccounts,
    IUnitOfWork unitOfWork,
    IClock clock,
    IAuditService audit)
{
    public Task HandleSuccessAsync(Guid paymentId, string billerReference, CancellationToken ct) =>
        ApplyAsync(paymentId, "Success", billerReference, ct);

    public Task HandleDeclineAsync(Guid paymentId, string reason, CancellationToken ct) =>
        ApplyAsync(paymentId, "Declined", reason, ct);

    public Task HandleUnknownAsync(Guid paymentId, CancellationToken ct) =>
        ApplyAsync(paymentId, "Unknown", null, ct);

    private async Task ApplyAsync(Guid paymentId, string outcome, string? detail, CancellationToken ct)
    {
        var payment = await db.BillPayments
            .SingleOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new NotFoundException("Bill payment not found.");

        if (payment.Status is not (BillPaymentStatus.Pending or BillPaymentStatus.RequiresReview))
        {
            throw new ConflictException($"Cannot apply a biller outcome to a payment in status {payment.Status}.");
        }

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            switch (outcome)
            {
                case "Success":
                    payment.Complete(detail!, null, clock.UtcNow);
                    break;

                case "Declined":
                    var refund = await PostRefundAsync(payment, detail ?? "Biller declined", token);
                    payment.Fail(detail ?? "Biller declined", refund.Id, clock.UtcNow);
                    break;

                case "Unknown":
                    payment.RequiresReview("Biller outcome unknown", clock.UtcNow);
                    break;
            }

            payment.IncrementAttempt();
            await unitOfWork.SaveChangesAsync(token);

            await audit.RecordAsync(
                $"BillPayment.Outcome.{outcome}",
                "BillPayment",
                payment.Id.ToString(),
                outcome == "Success" ? "Success" : outcome == "Declined" ? "Failed" : "RequiresReview",
                new { detail }, token);

            return true;
        }, ct, IsolationLevel.Serializable);
    }

    private async Task<JournalEntry> PostRefundAsync(BillPayment payment, string reason, CancellationToken ct)
    {
        var walletAccount = await ledgerAccounts.FindByWalletIdAsync(payment.WalletId, ct)
            ?? throw new NotFoundException("Wallet ledger account not found.");
        var settlementAccount = await ledgerAccounts.FindBySystemCodeAsync(
            SystemAccount.BillerSettlement, payment.Amount.Currency, ct)
            ?? throw new NotFoundException("Biller settlement account not found.");

        await locker.LockAsync(ct, walletAccount.Id, settlementAccount.Id);

        var entry = JournalEntry.Create(
                type: "BillPayment:Refund",
                createdBy: "BillPaymentOutcomeHandler",
                createdAtUtc: clock.UtcNow,
                reference: $"bill-refund:{payment.Id}",
                description: reason)
            .Debit(settlementAccount, payment.Amount)
            .Credit(walletAccount, payment.Amount)
            .Build();

        await ((LedgerService)ledger).PostAsyncInternal(entry, ct);
        return entry;
    }
}
