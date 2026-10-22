using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class LedgerAccountRepository(WalletDbContext dbContext) : ILedgerAccountRepository
{
    public Task<LedgerAccount?> FindByWalletIdAsync(Guid walletId, CancellationToken cancellationToken) =>
        dbContext.LedgerAccounts
            .SingleOrDefaultAsync(account => account.WalletId == walletId, cancellationToken);

    public Task<LedgerAccount?> FindBySystemCodeAsync(
        SystemAccount systemCode,
        Currency currency,
        CancellationToken cancellationToken) =>
        dbContext.LedgerAccounts.SingleOrDefaultAsync(
            account => account.SystemCode == systemCode && account.Currency == currency,
            cancellationToken);

    public async Task<IReadOnlyList<LedgerAccount>> GetSystemAccountsAsync(CancellationToken cancellationToken) =>
        await dbContext.LedgerAccounts
            .Where(account => account.SystemCode != null)
            .ToListAsync(cancellationToken);

    public void Add(LedgerAccount account) => dbContext.LedgerAccounts.Add(account);
}
