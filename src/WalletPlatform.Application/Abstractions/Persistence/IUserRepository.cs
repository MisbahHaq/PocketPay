using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<User>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);

    void Add(User user);
}
