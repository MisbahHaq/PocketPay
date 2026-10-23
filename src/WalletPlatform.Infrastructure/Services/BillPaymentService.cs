using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Services;

public sealed class BillPaymentService(
    WalletDbContext db,
    IAccountLocker locker)
{
    public async Task<BillPayment> AcceptAsync(Guid walletId, string billerCode, string consumerNumber, long amountMinorUnits, Guid userId, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var accounts = await db.LedgerAccounts.Where(a => a.WalletId == walletId).ToListAsync(ct);
            var acct = accounts.First();
            await locker.LockAsync(acct.Id);
            // basic validation omitted for brevity; funds check assumed
            var payment = BillPayment.Create(walletId, billerCode, consumerNumber, new Money(amountMinorUnits, Domain.Enums.Currency.PKR), DateTimeOffset.UtcNow);
            db.BillPayments.Add(payment);
            // post journal entry via LedgerService skipped in scaffold; outbox insert
            db.OutboxMessages.Add(new Data.Entities.OutboxMessage { Type = "SubmitBillPayment", Payload = $"{{\"paymentId\":\"{payment.Id}\"}}", CorrelationId = payment.Id });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return payment;
        });
    }
}
