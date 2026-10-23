using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Domain.Entities;

public sealed class BillPayment
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public string BillerCode { get; private set; } = default!;
    public string ConsumerNumber { get; private set; } = default!;
    public Money Amount { get; private set; } = null!;
    public BillPaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? BillerReference { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public Guid? RefundJournalEntryId { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BillPayment() { }

    public static BillPayment Create(Guid walletId, string billerCode, string consumerNumber, Money amount, DateTimeOffset createdAt)
    {
        if (walletId == Guid.Empty) throw new BillPaymentException("Wallet id is required.");
        if (string.IsNullOrWhiteSpace(billerCode)) throw new BillPaymentException("Biller code is required.");
        if (string.IsNullOrWhiteSpace(consumerNumber)) throw new BillPaymentException("Consumer number is required.");
        ArgumentNullException.ThrowIfNull(amount);
        if (amount.MinorUnits <= 0) throw new BillPaymentException("Amount must be greater than zero.");
        return new BillPayment
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            BillerCode = billerCode,
            ConsumerNumber = consumerNumber,
            Amount = amount,
            Status = BillPaymentStatus.Pending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Complete(string billerReference, Guid? journalEntryId, DateTimeOffset at)
    {
        if (Status != BillPaymentStatus.Pending && Status != BillPaymentStatus.RequiresReview)
            throw new BillPaymentException($"Cannot complete a payment in status {Status}.");
        Status = BillPaymentStatus.Completed;
        BillerReference = billerReference;
        if (journalEntryId.HasValue) JournalEntryId = journalEntryId;
        UpdatedAt = at;
    }

    public void Fail(string reason, Guid? refundJournalEntryId, DateTimeOffset at)
    {
        if (Status != BillPaymentStatus.Pending && Status != BillPaymentStatus.RequiresReview)
            throw new BillPaymentException($"Cannot fail a payment in status {Status}.");
        Status = BillPaymentStatus.Failed;
        FailureReason = reason;
        RefundJournalEntryId = refundJournalEntryId;
        UpdatedAt = at;
    }

    public void RequiresReview(string reason, DateTimeOffset at)
    {
        if (Status != BillPaymentStatus.Pending)
            throw new BillPaymentException($"Cannot mark RequiresReview from status {Status}.");
        Status = BillPaymentStatus.RequiresReview;
        FailureReason = reason;
        UpdatedAt = at;
    }

    public void IncrementAttempt() => AttemptCount++;
}
