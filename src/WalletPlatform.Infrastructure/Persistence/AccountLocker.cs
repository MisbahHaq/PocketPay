using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

/// <summary>
/// Implements pessimistic locking on ledger accounts using SELECT ... FOR UPDATE.
/// Locks are acquired in ascending account ID order to prevent deadlocks.
/// The lock is held within the current EF Core transaction.
/// </summary>
internal sealed class AccountLocker(WalletDbContext dbContext) : IAccountLocker
{
    public async Task LockAsync(params Guid[] ledgerAccountIds)
    {
        if (ledgerAccountIds is null || ledgerAccountIds.Length == 0)
        {
            return;
        }

        // Sort by ID to ensure consistent lock ordering and prevent deadlocks
        var sortedIds = ledgerAccountIds.Distinct().OrderBy(id => id).ToArray();

        // Build the FOR UPDATE query
        // We select from LedgerAccounts where Id is in the list, with FOR UPDATE
        var idsList = string.Join(",", sortedIds.Select(id => $"'{id}'"));
        
        var sql = $"""
            SELECT "Id" FROM "LedgerAccounts" 
            WHERE "Id" IN ({idsList})
            FOR UPDATE
            """;

        // Execute the raw SQL to acquire locks
        // This participates in the current EF Core transaction
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }
}