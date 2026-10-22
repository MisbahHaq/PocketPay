using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.UnitTests.Fakes;

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(user => user.Email == normalizedEmail));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(user => user.Id == id));

    public Task<PagedResult<User>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = Items
            .OrderBy(user => user.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<User>(items, Items.Count, page, pageSize));
    }

    public void Add(User user) => Items.Add(user);
}
