using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

/// <summary>
/// The double-entry ledger service. All balances are DERIVED from postings — there is no
/// stored balance column anywhere, because a cached balance can drift and can never be
/// rebuilt. Read paths therefore sum postings on demand (indexed by account + time).
/// </summary>
internal sealed class LedgerService(WalletDbContext dbContext, IUnitOfWork unitOfWork) : ILedgerService
{
    public async Task PostAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(journalEntry);

        // The entry and its postings are inserted together and committed atomically. If
        // anything fails, no posting survives — the ledger never holds half a journal entry.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            dbContext.JournalEntries.Add(journalEntry);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }

    public async Task<long?> GetBalanceAsync(
        Guid ledgerAccountId,
        CancellationToken cancellationToken = default)
    {
        var accountType = await dbContext.LedgerAccounts
            .Where(account => account.Id == ledgerAccountId)
            .Select(account => (AccountType?)account.Type)
            .SingleOrDefaultAsync(cancellationToken);

        if (accountType is null)
        {
            return null;
        }

        return await SumSignedAsync(ledgerAccountId, accountType.Value, cancellationToken);
    }

    public async Task<LedgerStatement> GetStatementAsync(
        Guid ledgerAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.LedgerAccounts
            .Where(candidate => candidate.Id == ledgerAccountId)
            .Select(candidate => new { candidate.Id, candidate.WalletId, candidate.Currency, candidate.Type })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ledger account not found.");

        var totalCount = await dbContext.LedgerEntries
            .CountAsync(entry => entry.LedgerAccountId == ledgerAccountId, cancellationToken);

        // The running balance is computed in the DATABASE with a window function so we do
        // not load the entire history into memory just to sum it. The sign of a posting
        // depends on whether the account is credit-normal (liability/revenue) or
        // debit-normal (asset):
        //   credit-normal: credit = +amount, debit = -amount
        //   debit-normal : debit  = +amount, credit = -amount
        // This is the same rule as LedgerEntry.SignedAmountForCreditNormal/DebitNormal.
        var favorableDirection = account.Type.IsCreditNormal() ? "Credit" : "Debit";
        var signedExpression =
            $"CASE WHEN e.\"Direction\" = '{favorableDirection}' " +
            "THEN e.\"AmountMinorUnits\" ELSE -e.\"AmountMinorUnits\" END";

        var skip = (page - 1) * pageSize;

        var sql =
            "SELECT " +
            "e.\"JournalEntryId\" AS \"JournalEntryId\", " +
            "e.\"CreatedAtUtc\" AS \"CreatedAtUtc\", " +
            "e.\"Direction\" AS \"Direction\", " +
            "e.\"AmountMinorUnits\" AS \"AmountMinorUnits\", " +
            "e.\"Type\" AS \"Type\", " +
            "e.\"Reference\" AS \"Reference\", " +
            "e.\"Description\" AS \"Description\", " +
            "SUM(" + signedExpression + ") OVER (" +
            "ORDER BY e.\"CreatedAtUtc\", e.\"Id\" " +
            "ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW" +
            ") AS \"RunningBalanceMinorUnits\" " +
            "FROM (" +
            "SELECT " +
            "le.\"JournalEntryId\", " +
            "le.\"CreatedAtUtc\", " +
            "le.\"Id\", " +
            "le.\"Direction\", " +
            "le.\"AmountMinorUnits\", " +
            "je.\"Type\" AS \"Type\", " +
            "je.\"Reference\" AS \"Reference\", " +
            "je.\"Description\" AS \"Description\" " +
            "FROM \"LedgerEntries\" le " +
            "JOIN \"JournalEntries\" je ON je.\"Id\" = le.\"JournalEntryId\" " +
            "WHERE le.\"LedgerAccountId\" = {0}" +
            ") e " +
            "ORDER BY e.\"CreatedAtUtc\", e.\"Id\" " +
            "OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY";

        var lines = await dbContext.Database
            .SqlQueryRaw<StatementRow>(sql, ledgerAccountId, skip, pageSize)
            .ToListAsync(cancellationToken);

        // The closing balance is the signed sum of ALL postings, independent of the page.
        var closingBalance = await SumSignedAsync(ledgerAccountId, account.Type, cancellationToken);

        var mapped = lines
            .Select(line => new LedgerStatementLine(
                line.JournalEntryId,
                line.CreatedAtUtc,
                Enum.Parse<Direction>(line.Direction),
                line.AmountMinorUnits,
                line.RunningBalanceMinorUnits,
                line.Type,
                line.Reference,
                line.Description))
            .ToList();

        return new LedgerStatement(
            account.Id,
            account.WalletId,
            account.Currency,
            closingBalance,
            page,
            pageSize,
            totalCount,
            mapped);
    }

    private Task<long> SumSignedAsync(
        Guid ledgerAccountId,
        AccountType accountType,
        CancellationToken cancellationToken)
    {
        var postings = dbContext.LedgerEntries
            .Where(entry => entry.LedgerAccountId == ledgerAccountId);

        return accountType.IsCreditNormal()
            ? postings.SumAsync(entry => entry.Direction == Direction.Credit
                ? entry.AmountMinorUnits
                : -entry.AmountMinorUnits, cancellationToken)
            : postings.SumAsync(entry => entry.Direction == Direction.Debit
                ? entry.AmountMinorUnits
                : -entry.AmountMinorUnits, cancellationToken);
    }

    private sealed class StatementRow
    {
        public Guid JournalEntryId { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public string Direction { get; init; } = null!;
        public long AmountMinorUnits { get; init; }
        public long RunningBalanceMinorUnits { get; init; }
        public string? Type { get; init; }
        public string? Reference { get; init; }
        public string? Description { get; init; }
    }
}
