using InventoryApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryApp.Tests.Support;

/// <summary>
/// Builds a real <see cref="UserManager{TUser}"/> over a mocked store. UserManager is concrete and
/// awkward to mock, so this is the standard approach: the manager under test is genuine, only
/// storage is faked. Moq's As&lt;T&gt;() adds the role, email and security-stamp store interfaces to
/// the one proxy, because UserManager casts that single object to each of them at runtime.
/// </summary>
internal sealed class TestUserManagerHarness
{
    private readonly List<ApplicationUser> _users = [];
    private readonly Dictionary<string, IList<string>> _roles = new(StringComparer.Ordinal);

    /// <summary>Users currently in the fake store, for tests that need to look an account up.</summary>
    public IReadOnlyList<ApplicationUser> Users => _users;

    public Mock<IUserStore<ApplicationUser>> Store { get; } = new();

    public PasswordHasher<ApplicationUser> PasswordHasher { get; } = new();

    public UserManager<ApplicationUser> UserManager { get; }

    public TestUserManagerHarness()
    {
        var emailStore = Store.As<IUserEmailStore<ApplicationUser>>();
        var roleStore = Store.As<IUserRoleStore<ApplicationUser>>();
        var securityStampStore = Store.As<IUserSecurityStampStore<ApplicationUser>>();

        Store.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string userId, CancellationToken _) =>
                Task.FromResult(_users.FirstOrDefault(user => user.Id == userId)));

        emailStore.Setup(store => store.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string normalizedEmail, CancellationToken _) =>
                Task.FromResult(_users.FirstOrDefault(user => user.NormalizedEmail == normalizedEmail)));

        roleStore.Setup(store => store.GetRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .Returns((ApplicationUser user, CancellationToken _) =>
                Task.FromResult(_roles.TryGetValue(user.Id, out var roles)
                    ? roles
                    : (IList<string>)new List<string>()));

        securityStampStore
            .Setup(store => store.SetSecurityStampAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Store.Setup(store => store.UpdateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var options = Options.Create(new IdentityOptions
        {
            Password =
            {
                RequiredLength = 12,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = true,
                RequiredUniqueChars = 4
            },
            Lockout =
            {
                AllowedForNewUsers = true,
                MaxFailedAccessAttempts = 5,
                DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)
            },
            User = { RequireUniqueEmail = true }
        });

        UserManager = new UserManager<ApplicationUser>(
            store: Store.Object,
            optionsAccessor: options,
            passwordHasher: PasswordHasher,
            userValidators: [],
            passwordValidators: [new PasswordValidator<ApplicationUser>()],
            keyNormalizer: new UpperInvariantLookupNormalizer(),
            errors: new IdentityErrorDescriber(),
            services: Mock.Of<IServiceProvider>(),
            logger: NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    /// <summary>Registers a user whose password hash is a genuine Identity hash of <paramref name="password"/>.</summary>
    public ApplicationUser AddUser(string password, bool isActive = true, params string[] roles)
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = "Test User",
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = isActive,
            PasswordHash = PasswordHasher.HashPassword(new ApplicationUser(), password)
        };

        _users.Add(user);
        _roles[user.Id] = [.. roles];
        return user;
    }
}