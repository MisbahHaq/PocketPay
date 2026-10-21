using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

internal sealed class UserRepository(WalletDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<PagedResult<User>> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.Users.CountAsync(cancellationToken);
        var items = await dbContext.Users
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, totalCount, page, pageSize);
    }

    public void Add(User user) => dbContext.Users.Add(user);
}
