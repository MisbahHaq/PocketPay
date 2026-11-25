using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Abstractions.Services;
using WalletPlatform.Application.Common;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Transfers.Policies;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
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
    private const int MaxConcurrencyRetries = 5;

    public async Task<Transfer> ExecuteAsync(
        Guid sourceWalletId,
        string destinationWalletNumber,
        Money amount,
        string? note,
        CancellationToken cancellationToken = default)
    {
        // Reference data lookups (wallets, ledger accounts) are safe outside the
        // transaction — they are not mutated by a transfer. All mutable state
        // (balances, daily totals) is checked INSIDE the serializable transaction
        // while holding row locks, eliminating the check-then-act race.
        var sourceWallet = await walletRepository.FindByIdAsync(sourceWalletId, cancellationToken)
            ?? throw new NotFoundException("Source wallet not found.");

        var destinationWallet = await walletRepository.FindByWalletNumberAsync(destinationWalletNumber, cancellationToken)
            ?? throw new NotFoundException($"Destination wallet '{destinationWalletNumber}' not found.");

        var destinationCheck = new DestinationWalletPolicy().Check(
            sourceWalletId, destinationWallet.Id, destinationWalletNumber);
        if (!destinationCheck.IsSuccess)
        {
            return await PersistFailedTransferAsync(
                sourceWalletId, destinationWallet.Id, amount, note, destinationCheck, cancellationToken);
        }

        var currencyCheck = new CurrencyMatchPolicy().Check(sourceWallet.Currency, destinationWallet.Currency);
        if (!currencyCheck.IsSuccess)
        {
            return await PersistFailedTransferAsync(
                sourceWalletId, destinationWallet.Id, amount, note, currencyCheck, cancellationToken);
        }

        var sourceAccount = await ledgerAccountRepository.FindByWalletIdAsync(sourceWalletId, cancellationToken)
            ?? throw new NotFoundException("Source ledger account not found.");
        var destinationAccount = await ledgerAccountRepository.FindByWalletIdAsync(destinationWallet.Id, cancellationToken)
            ?? throw new NotFoundException("Destination ledger account not found.");

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ExecuteInSerializableTransactionAsync(
                    sourceWallet, destinationWallet, sourceAccount, destinationAccount,
                    amount, note, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsTransientConcurrencyFailure(ex) && attempt < MaxConcurrencyRetries)
            {
                // Serializable isolation can abort one of two conflicting transactions
                // with 40001 (serialization failure) or 40P01 (deadlock). Retrying is
                // safe because the whole unit of work was rolled back — no partial
                // transfer or journal entry can survive.
                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)), cancellationToken);
            }
        }
    }

    private async Task<Transfer> ExecuteInSerializableTransactionAsync(
        Wallet sourceWallet,
        Wallet destinationWallet,
        LedgerAccount sourceAccount,
        LedgerAccount destinationAccount,
        Money amount,
        string? note,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Acquire pessimistic locks on BOTH accounts in consistent order (by ID).
            // Combined with SERIALIZABLE isolation, this prevents TOCTOU races and
            // deadlocks between concurrent transfers.
            await accountLocker.LockAsync(token, sourceAccount.Id, destinationAccount.Id);

            // Re-read all mutable state UNDER THE LOCK.
            var currentBalance = await ledgerService.GetBalanceAsync(sourceAccount.Id, token)
                ?? throw new NotFoundException("Source ledger account balance unavailable.");

            var fee = new Money(options.Value.FlatFeeMinorUnits, amount.Currency);
            var amountCheck = new AmountLimitsPolicy(options.Value).Check(amount, fee, currentBalance);
            if (!amountCheck.IsSuccess)
            {
                return await PersistFailedTransferInTransactionAsync(
                    sourceWallet.Id, destinationWallet.Id, amount, note, amountCheck, token);
            }

            var dailySum = await transferRepository.GetCompletedOutgoingSumAsync(
                sourceWallet.Id, clock.UtcNow.AddHours(-24), token);
            var dailyCheck = new DailyLimitPolicy(options.Value, clock).Check(dailySum, amount, fee);
            if (!dailyCheck.IsSuccess)
            {
                return await PersistFailedTransferInTransactionAsync(
                    sourceWallet.Id, destinationWallet.Id, amount, note, dailyCheck, token);
            }

            var journalEntry = JournalEntry.Create(
                type: "Transfer",
                createdBy: sourceWallet.UserId.ToString(),
                createdAtUtc: clock.UtcNow,
                reference: $"txn:{sourceWallet.Id}->{destinationWallet.Id}");

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
            await ((LedgerService)ledgerService).PostAsyncInternal(journalEntry, token);

            var transfer = Transfer.Create(
                sourceWallet.Id,
                destinationWallet.Id,
                amount,
                fee,
                note,
                clock.UtcNow);
            transfer.Complete(journalEntry.Id, clock.UtcNow);

            await transferRepository.AddAsync(transfer, token);
            await unitOfWork.SaveChangesAsync(token);

            return transfer;
        }, cancellationToken, IsolationLevel.Serializable);
    }

    private async Task<Transfer> PersistFailedTransferInTransactionAsync(
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

    private static bool IsTransientConcurrencyFailure(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected,
        };
}
