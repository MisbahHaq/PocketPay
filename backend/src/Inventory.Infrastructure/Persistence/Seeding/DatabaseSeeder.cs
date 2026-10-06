using Inventory.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Persistence.Seeding;

/// <summary>
/// Provisions the three system roles, and optionally one administrator.
/// </summary>
/// <remarks>
/// <para>
/// Roles are seeded idempotently on every start. They are a fixed, code-defined
/// vocabulary rather than user data, so the application cannot boot without them -
/// an unrecognised role in a policy is a silent authorization failure, and the fix
/// should not require a database edit.
/// </para>
/// <para>
/// The admin is only created when <c>Seed:AdminEmail</c> and <c>Seed:AdminPassword</c>
/// are both supplied, and it is <b>refused outright outside Development</b>. This
/// codebase will not invent a default credential: a seeder that quietly creates
/// "admin/admin" is how demo defaults end up in production. Credentials come from user
/// secrets locally and from environment variables in a deployment.
/// </para>
/// </remarks>
public sealed class DatabaseSeeder(
    InventoryDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger,
    IHostEnvironment environment)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying database migrations");

        // Pending migrations rather than EnsureCreated: migrations are the only schema
        // history we keep, and EnsureCreated would bypass them silently on an empty DB.
        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database schema is up to date");
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedAdministratorAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var role in SystemRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid> { Id = Domain.Common.SequentialGuid.New(), Name = role });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed role '{role}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }

            logger.LogInformation("Seeded role {Role}", role);
        }
    }

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        var fullName = configuration["Seed:AdminFullName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "No seed administrator configured (set Seed:AdminEmail and Seed:AdminPassword via user secrets to create one)");
            return;
        }

        if (!environment.IsDevelopment())
        {
            // Belt and braces: even if a secret is accidentally present in a deployed
            // environment, refuse to create a well-known administrator.
            throw new InvalidOperationException(
                "Refusing to seed an administrator outside the Development environment. " +
                "Provision the first account manually, or run the seeder as a one-off job.");
        }

        email = email.Trim();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            logger.LogInformation("Seed administrator {Email} already exists; leaving it untouched", email);
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Domain.Common.SequentialGuid.New(),
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LockoutEnabled = true,
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not seed administrator '{email}': {string.Join("; ", created.Errors.Select(error => error.Description))}");
        }

        var assigned = await userManager.AddToRoleAsync(admin, SystemRoles.Admin);
        if (!assigned.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the Admin role to '{email}': {string.Join("; ", assigned.Errors.Select(error => error.Description))}");
        }

        logger.LogWarning(
            "Seeded administrator {Email}. Change this password immediately and remove the seed secrets.",
            email);
    }
}