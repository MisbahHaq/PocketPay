using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// Idempotent startup seeding. Roles always exist; the admin account exists only when credentials
/// are supplied through configuration (user-secrets outside development).
/// </summary>
internal sealed class DatabaseSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var created = await roleManager.CreateAsync(new IdentityRole(role));
                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        $"Could not seed role '{role}': {string.Join(" ", created.Errors.Select(error => error.Description))}");
            }
        }

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        // Refusing to invent a default password is deliberate: a hardcoded fallback would end up in
        // git history and in every deployment that forgot to override it.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Skipping admin seeding. Set SeedAdmin:Email and SeedAdmin:Password, for example: "
                + "dotnet user-secrets set \"SeedAdmin:Password\" \"<strong password>\"");
            return;
        }

        email = email.Trim().ToUpperInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            logger.LogInformation("Seed admin {Email} already exists.", email);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = configuration["SeedAdmin:FullName"] ?? "System Administrator",
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        var createdAdmin = await userManager.CreateAsync(admin, password);
        if (!createdAdmin.Succeeded)
            throw new InvalidOperationException(
                $"Could not seed the admin user: {string.Join(" ", createdAdmin.Errors.Select(error => error.Description))}");

        var roleAssigned = await userManager.AddToRoleAsync(admin, SystemRoles.Admin);
        if (!roleAssigned.Succeeded)
            throw new InvalidOperationException(
                $"Could not assign the Admin role: {string.Join(" ", roleAssigned.Errors.Select(error => error.Description))}");

        logger.LogInformation("Seeded admin user {Email}.", email);
    }
}
