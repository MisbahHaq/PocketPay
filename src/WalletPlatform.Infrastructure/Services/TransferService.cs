using Microsoft.Extensions.Options;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Abstractions.Services;
using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers.Policies;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.Infrastructure.Persistence;

namespace WalletPlatform.Infrastructure.Services;

internal sealed class TransferService(
    IWalletRepository walletRepository,
    ILedgerAccountRepository ledgerAccountRepository,
    ITransferRepository transferRepository,
    ILedgerService ledgerService,
    IAccountLocker accountLocker,
    IUnitOfWork unitOfWork,
    IOptions<TransferOptions> options,
    IClock clock) : ITransferService
{
    public async Task<Transfer> ExecuteAsync(
        Guid sourceWalletId,
        string destinationWalletNumber,
        Money amount,
        string? note,
        CancellationToken cancellationToken = default)
    {
        // Look up both wallets and their ledger accounts.
        var sourceWallet = await walletRepository.FindByIdAsync(sourceWalletId, cancellationToken)
            ?? throw new NotFoundException("Source wallet not found.");

        var destinationWallet = await walletRepository.FindByWalletNumberAsync(destinationWalletNumber, cancellationToken)
            ?? throw new NotFoundException($"Destination wallet '{destinationWalletNumber}' not found.");

        // 2. Destination exists and differs from source
        var destinationCheck = new DestinationWalletPolicy().Check(
            sourceWalletId, destinationWallet.Id, destinationWalletNumber);
        if (!destinationCheck.IsSuccess)
        {
            return await PersistFailedTransferAsync(
                sourceWalletId, destinationWallet.Id, amount, note, destinationCheck, cancellationToken);
        }

        // 3. Currency match
        var currencyCheck = new CurrencyMatchPolicy().Check(sourceWallet.Currency, destinationWallet.Currency);
        if (!currencyCheck.IsSuccess)
        {
            return await PersistFailedTransferAsync(
                sourceWalletId, destinationWallet.Id, amount, note, currencyCheck, cancellationToken);
        }

        // Get ledger accounts
        var sourceAccount = await ledgerAccountRepository.FindByWalletIdAsync(sourceWalletId, cancellationToken)
            ?? throw new NotFoundException("Source ledger account not found.");
        var destinationAccount = await ledgerAccountRepository.FindByWalletIdAsync(destinationWallet.Id, cancellationToken)
            ?? throw new NotFoundException("Destination ledger account not found.");

        // 4. Amount limits + available balance
        var availableBalance = await ledgerService.GetBalanceAsync(sourceAccount.Id, cancellationToken)
            ?? throw new NotFoundException("Source ledger account balance unavailable.");
        var fee = new Money(options.Value.FlatFeeMinorUnits, amount.Currency);
        var amountCheck = new AmountLimitsPolicy(options.Value).Check(amount, fee, availableBalance);
        if (!amountCheck.IsSuccess)
        {
            return await PersistFailedTransferAsync(
                sourceWalletId, destinationWallet.Id, amount, note, amountCheck, cancellationToken);
        }

        // 5. Daily outgoing limit (completed transfers in last 24h)
        var dailySum = await transferRepository.GetCompletedOutgoingSumAsync(sourceWalletId, clock.UtcNow.AddHours(-24), cancellationToken);
        var dailyCheck = new DailyLimitPolicy(options.Value, clock).Check(dailySum, amount, fee);
        if (!dailyCheck.IsSuccess)
        {
            return await PersistFailedTransferAsync(
                sourceWalletId, destinationWallet.Id, amount, note, dailyCheck, cancellationToken);
        }

        // All policies passed — execute the transfer + journal entry in one transaction
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Acquire pessimistic locks on BOTH accounts in consistent order (by ID)
            // This prevents:
            // 1. TOCTOU: balance/daily-limit checks and posting happen while lock is held
            // 2. Deadlocks: AccountLocker sorts IDs globally before locking
            await accountLocker.LockAsync(sourceAccount.Id, destinationAccount.Id);

            // Re-read the balance inside the transaction UNDER THE LOCK
            var currentBalance = await ledgerService.GetBalanceAsync(sourceAccount.Id, token)
                ?? throw new NotFoundException("Source ledger account balance unavailable.");

            var recheck = new AmountLimitsPolicy(options.Value).Check(amount, fee, currentBalance);
            if (!recheck.IsSuccess)
            {
                var failed = CreateFailedTransfer(sourceWalletId, destinationWallet.Id, amount, note, recheck);
                await transferRepository.AddAsync(failed, token);
                await unitOfWork.SaveChangesAsync(token);
                return failed;
            }

            // Re-check daily limit under the lock
            var dailySum = await transferRepository.GetCompletedOutgoingSumAsync(sourceWalletId, clock.UtcNow.AddHours(-24), token);
            var dailyRecheck = new DailyLimitPolicy(options.Value, clock).Check(dailySum, amount, fee);
            if (!dailyRecheck.IsSuccess)
            {
                var failed = CreateFailedTransfer(sourceWalletId, destinationWallet.Id, amount, note, dailyRecheck);
                await transferRepository.AddAsync(failed, token);
                await unitOfWork.SaveChangesAsync(token);
                return failed;
            }

            // Build the journal entry:
            // Debit source wallet for amount + fee
            // Credit destination wallet for amount
            // Credit SYSTEM_FEES for fee (if > 0)
            var journalEntry = JournalEntry.Create(
                type: "Transfer",
                createdBy: sourceWallet.UserId.ToString(),
                createdAtUtc: clock.UtcNow,
                reference: $"txn:{sourceWalletId}->{destinationWallet.Id}");

            journalEntry.Debit(sourceAccount, amount + fee);
            journalEntry.Credit(destinationAccount, amount);

            if (fee.MinorUnits > 0)
            {
                var feesAccount = await ledgerAccountRepository.FindBySystemCodeAsync(
                    SystemAccount.Fees, amount.Currency, token)
                    ?? throw new NotFoundException("System fees account not found.");
                journalEntry.Credit(feesAccount, fee);
            }

            journalEntry.Build();
            // Use internal method to participate in the outer transaction
            await ((LedgerService)ledgerService).PostAsyncInternal(journalEntry, token);

            // Create and persist the transfer marked Completed
            var transfer = Transfer.Create(
                sourceWalletId,
                destinationWallet.Id,
                amount,
                fee,
                note,
                clock.UtcNow);
            transfer.Complete(journalEntry.Id, clock.UtcNow);

            await transferRepository.AddAsync(transfer, token);

            // Save the transfer in the same transaction
            await unitOfWork.SaveChangesAsync(token);

            return transfer;
        }, cancellationToken);
    }

    private async Task<Transfer> PersistFailedTransferAsync(
        Guid sourceWalletId,
        Guid destinationWalletId,
        Money amount,
        string? note,
        PolicyResult result,
        CancellationToken cancellationToken)
    {
        var failed = CreateFailedTransfer(sourceWalletId, destinationWalletId, amount, note, result);
        await transferRepository.AddAsync(failed, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return failed;
    }

    private Transfer CreateFailedTransfer(
        Guid sourceWalletId,
        Guid destinationWalletId,
        Money amount,
        string? note,
        PolicyResult result)
    {
        var fee = new Money(options.Value.FlatFeeMinorUnits, amount.Currency);
        var transfer = Transfer.Create(sourceWalletId, destinationWalletId, amount, fee, note, clock.UtcNow);
        transfer.Fail(result.Code!);
        return transfer;
    }
}