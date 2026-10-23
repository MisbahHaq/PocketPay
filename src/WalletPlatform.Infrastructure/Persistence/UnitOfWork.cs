using Microsoft.EntityFrameworkCore;
using Npgsql;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class UnitOfWork(WalletDbContext dbContext) : IUnitOfWork
{
    // Internal for TransferService to add the transfer in the same transaction.
    internal WalletDbContext DbContext => dbContext;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesCoreAsync(cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<int> SaveChangesCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } postgresException)
        {
            // Translate unique-index violations into a domain-level conflict so the
            // transport layer can return 409 without knowing about Postgres.
            var message = postgresException.ConstraintName switch
            {
                "IX_Users_Email" => "An account with this email already exists.",
                "IX_Wallets_WalletNumber" => "A wallet with this number already exists.",
                "IX_JournalEntries_ReversesJournalEntryId" => "This journal entry has already been reversed.",
                _ => "The request conflicts with an existing record.",
            };

            throw new ConflictException(message);
        }
    }
}
