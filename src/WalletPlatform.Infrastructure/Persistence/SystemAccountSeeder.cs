using Microsoft.Extensions.Logging;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Infrastructure.Persistence;

/// <summary>
/// Ensures the platform's system ledger accounts (funding, fees, biller settlement)
/// exist for every supported currency. Seeding is idempotent: it looks each account up
/// by its (code, currency) pair and only inserts the ones that are missing, so it can run
/// on every startup without creating duplicates. The database's unique filtered index on
/// (SystemCode, Currency) is the backstop if two instances race.
/// </summary>
public sealed class SystemAccountSeeder(
    ILedgerAccountRepository ledgerAccounts,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<SystemAccountSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await ledgerAccounts.GetSystemAccountsAsync(cancellationToken);
        var existingKeys = existing
            .Select(account => (account.SystemCode!.Value, account.Currency))
            .ToHashSet();

        var created = 0;
        var now = clock.UtcNow;

        foreach (var currency in Enum.GetValues<Currency>())
        {
            foreach (var systemCode in Enum.GetValues<SystemAccount>())
            {
                if (existingKeys.Contains((systemCode, currency)))
                {
                    continue;
                }

                ledgerAccounts.Add(LedgerAccount.CreateSystem(systemCode, currency, now));
                created++;
            }
        }

        if (created == 0)
        {
            logger.LogInformation("SystemAccountSeed: all system accounts already exist; nothing to do.");
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("SystemAccountSeed: created {Count} system ledger account(s).", created);
    }
}
