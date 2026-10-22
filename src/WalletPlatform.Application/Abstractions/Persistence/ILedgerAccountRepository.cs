using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Application.Abstractions.Persistence;

public interface ILedgerAccountRepository
{
    Task<LedgerAccount?> FindByWalletIdAsync(Guid walletId, CancellationToken cancellationToken);

    Task<LedgerAccount?> FindBySystemCodeAsync(
        SystemAccount systemCode,
        Currency currency,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerAccount>> GetSystemAccountsAsync(CancellationToken cancellationToken);

    void Add(LedgerAccount account);
}
