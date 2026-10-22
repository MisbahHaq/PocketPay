using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeLedgerAccountRepository : ILedgerAccountRepository
{
    public List<LedgerAccount> Items { get; } = [];

    public Task<LedgerAccount?> FindByWalletIdAsync(Guid walletId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(account => account.WalletId == walletId));

    public Task<LedgerAccount?> FindBySystemCodeAsync(
        SystemAccount systemCode,
        Currency currency,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(
            account => account.SystemCode == systemCode && account.Currency == currency));

    public Task<IReadOnlyList<LedgerAccount>> GetSystemAccountsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LedgerAccount>>(
            Items.Where(account => account.SystemCode is not null).ToList());

    public void Add(LedgerAccount account) => Items.Add(account);
}
