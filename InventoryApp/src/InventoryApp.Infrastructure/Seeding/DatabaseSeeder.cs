using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Domain.Constants;
using InventoryApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryApp.Infrastructure.Seeding;

/// <summary>
/// Idempotent startup seeding: the three roles always, and an administrator
/// only when credentials were supplied through configuration.
/// </summary>
public sealed class DatabaseSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<SeedAdminOptions> seedAdminOptions,
    TimeProvider timeProvider,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedAdminOptions _options = seedAdminOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Identity's async APIs have no CancellationToken overloads, so the
        // token is accepted for interface symmetry but not threaded further.
        await SeedRolesAsync();
        await SeedAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in ApplicationRoles.All)
        {
            // Read-then-create rather than RoleExistsAsync + CreateAsync: role
            // creation is effectively unique, so an existence check would still
            // race with a second instance starting up.
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new ConfigurationException(
                    $"Could not seed role '{role}': "
                    + string.Join("; ", result.Errors.Select(error => error.Description)));
            }

            logger.LogInformation("Seeded role {Role}.", role);
        }
    }

    private async Task SeedAdminAsync()
    {
        if (!_options.IsConfigured)
        {
            // Expected in CI and in a fresh clone. Not an error: there may be no
            // local administrator yet, and the app still starts.
            logger.LogWarning(
                "No bootstrap administrator configured. Set {Section}:Email and {Section}:Password "
                + "via user-secrets to create one on startup.",
                SeedAdminOptions.SectionName,
                SeedAdminOptions.SectionName);

            return;
        }

        var email = _options.Email.Trim().ToUpperInvariant();

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is not null)
        {
            // Only top up role membership. Resetting the password on every boot
            // would silently undo an intentional change.
            await EnsureAdminRoleAsync(admin);
            return;
        }

        admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = _options.FullName,
            CreatedAt = timeProvider.GetUtcNow(),
            IsActive = true,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(admin, _options.Password);
        if (!createResult.Succeeded)
        {
            throw new ConfigurationException(
                $"Could not seed the administrator '{email}'. Check that {SeedAdminOptions.SectionName}:Password "
                + "satisfies the password policy. Errors: "
                + string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);
        if (!roleResult.Succeeded)
        {
            throw new ConfigurationException(
                $"Could not assign the '{ApplicationRoles.Admin}' role to '{email}': "
                + string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        logger.LogInformation("Seeded administrator {Email}.", email);
    }

    private async Task EnsureAdminRoleAsync(ApplicationUser admin)
    {
        if (await userManager.IsInRoleAsync(admin, ApplicationRoles.Admin))
        {
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);
        if (!roleResult.Succeeded)
        {
            throw new ConfigurationException(
                $"Could not assign the '{ApplicationRoles.Admin}' role to '{admin.Email}': "
                + string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        logger.LogWarning(
            "User {Email} already existed and has been granted the {Role} role.",
            admin.Email,
            ApplicationRoles.Admin);
    }
}
