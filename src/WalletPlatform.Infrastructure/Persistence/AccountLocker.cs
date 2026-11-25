using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

/// <summary>
/// Implements pessimistic locking on ledger accounts using SELECT ... FOR UPDATE.
/// Locks are acquired in ascending account ID order to prevent deadlocks.
/// The lock is held within the current EF Core transaction.
/// </summary>
internal sealed class AccountLocker(WalletDbContext dbContext) : IAccountLocker
{
    public async Task LockAsync(CancellationToken cancellationToken, params Guid[] ledgerAccountIds)
    {
        if (ledgerAccountIds is null || ledgerAccountIds.Length == 0)
        {
            return;
        }

        // Sort by ID to ensure consistent lock ordering and prevent deadlocks
        var sortedIds = ledgerAccountIds.Distinct().OrderBy(id => id).ToArray();

        // Parameterized FOR UPDATE query — avoids SQL injection and lets Postgres
        // reuse the prepared statement. Participants participate in the current transaction.
        var parameters = sortedIds.Select((id, index) => $"@p{index}");
        var sql = $"""
            SELECT "Id" FROM "LedgerAccounts"
            WHERE "Id" IN ({string.Join(", ", parameters)})
            ORDER BY "Id"
            FOR UPDATE
            """;

        var values = sortedIds.Select(id => (object)id).ToArray();
        await dbContext.Database.ExecuteSqlRawAsync(sql, values, cancellationToken);
    }
}
