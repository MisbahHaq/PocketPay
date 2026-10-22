using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Security;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Infrastructure.Persistence;

public sealed class AdminSeedOptions
{
    public const string SectionName = "SeedAdmin";

    public string? Email { get; init; }

    public string? Password { get; init; }
}

/// <summary>
/// Creates the initial administrator on startup when SeedAdmin email/password are
/// configured. Both are expected to come from environment variables or user-secrets;
/// when they are absent (e.g. integration tests) seeding is skipped without touching
/// the database.
/// </summary>
public sealed class AdminSeeder(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IClock clock,
    IOptions<AdminSeedOptions> options,
    ILogger<AdminSeeder> logger)
{
    private readonly AdminSeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            logger.LogInformation("AdminSeed: skipping because SeedAdmin:Email/Password are not configured.");
            return;
        }

        if (!PasswordPolicy.IsSatisfiedBy(_options.Password))
        {
            logger.LogError("AdminSeed: skipping because the configured password does not satisfy the password policy.");
            return;
        }

        var email = User.NormalizeEmail(_options.Email);
        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            logger.LogInformation("AdminSeed: an account with the configured email already exists; skipping.");
            return;
        }

        users.Add(User.Create(email, passwordHasher.Hash(_options.Password), Role.Admin, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("AdminSeed: administrator account created.");
    }
}
