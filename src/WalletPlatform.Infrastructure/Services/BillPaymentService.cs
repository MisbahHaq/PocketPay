using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Audit;
using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Data.Entities;
using WalletPlatform.Infrastructure.Persistence;

namespace WalletPlatform.Infrastructure.Services;

public sealed class BillPaymentService(
    WalletDbContext db,
    IAccountLocker locker,
    ILedgerService ledger,
    ILedgerAccountRepository ledgerAccounts,
    IUnitOfWork unitOfWork,
    IClock clock,
    IAuditService audit)
{
    private const int MaxConcurrencyRetries = 5;

    public async Task<BillPayment> AcceptAsync(
        Guid walletId,
        string billerCode,
        string consumerNumber,
        long amountMinorUnits,
        Guid userId,
        CancellationToken ct)
    {
        var wallet = await db.Wallets
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == walletId, ct)
            ?? throw new NotFoundException("Wallet not found.");

        if (wallet.UserId != userId)
        {
            throw new NotFoundException("Wallet not found.");
        }

        var biller = await db.Billers
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Code == billerCode, ct)
            ?? throw new NotFoundException($"Biller '{billerCode}' not found.");

        if (!biller.Enabled)
        {
            throw new BillPaymentException($"Biller '{billerCode}' is disabled.");
        }

        if (!Regex.IsMatch(consumerNumber, biller.ConsumerNumberRegex))
        {
            throw new BillPaymentException("Consumer number is invalid for this biller.");
        }

        if (amountMinorUnits <= 0)
        {
            throw new BillPaymentException("Amount must be greater than zero.");
        }

        var sourceAccount = await ledgerAccounts.FindByWalletIdAsync(walletId, ct)
            ?? throw new NotFoundException("Wallet ledger account not found.");

        var amount = new Money(amountMinorUnits, wallet.Currency);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await AcceptInTransactionAsync(
                    wallet, biller, sourceAccount, amount, consumerNumber, ct);
            }
            catch (DbUpdateException ex) when (IsTransientConcurrencyFailure(ex) && attempt < MaxConcurrencyRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)), ct);
            }
        }
    }

    private async Task<BillPayment> AcceptInTransactionAsync(
        Wallet wallet,
        Biller biller,
        LedgerAccount sourceAccount,
        Money amount,
        string consumerNumber,
        CancellationToken ct)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await locker.LockAsync(token, sourceAccount.Id);

            var balance = await ledger.GetBalanceAsync(sourceAccount.Id, token)
                ?? throw new NotFoundException("Wallet ledger account balance unavailable.");

            if (balance < amount.MinorUnits)
            {
                throw new BillPaymentException("Insufficient funds to pay this bill.");
            }

            var settlementAccount = await ledgerAccounts.FindBySystemCodeAsync(
                SystemAccount.BillerSettlement, amount.Currency, token)
                ?? throw new InvalidOperationException(
                    $"The biller settlement account for {amount.Currency} has not been seeded.");

            var journalEntry = JournalEntry.Create(
                    type: "BillPayment",
                    createdBy: wallet.UserId.ToString(),
                    createdAtUtc: clock.UtcNow,
                    reference: $"bill:{biller.Code}:{consumerNumber}")
                .Debit(sourceAccount, amount)
                .Credit(settlementAccount, amount)
                .Build();

            await ((LedgerService)ledger).PostAsyncInternal(journalEntry, token);

            var payment = BillPayment.Create(
                wallet.Id, biller.Code, consumerNumber, amount, clock.UtcNow);
            payment.Complete(billerReference: string.Empty, journalEntry.Id, clock.UtcNow);
            db.BillPayments.Add(payment);

            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "SubmitBillPayment",
                Payload = JsonSerializer.Serialize(new
                {
                    payment.Id,
                    biller.Code,
                    consumerNumber,
                    amount.MinorUnits,
                    Currency = amount.Currency.ToString(),
                }),
                CorrelationId = payment.Id,
            });

            await unitOfWork.SaveChangesAsync(token);

            await audit.RecordAsync(
                "BillPayment.Accept", "BillPayment", payment.Id.ToString(), "Accepted",
                new { payment.WalletId, biller.Code, amount.MinorUnits }, token);

            return payment;
        }, ct, IsolationLevel.Serializable);
    }

    private static bool IsTransientConcurrencyFailure(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected,
        };
}
