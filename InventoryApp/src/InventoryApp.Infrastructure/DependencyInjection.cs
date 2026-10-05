using System.Text;
using InventoryApp.Application.Auth;
using InventoryApp.Application.Common.Interfaces;
using InventoryApp.Domain.Entities;
using InventoryApp.Infrastructure.DependencyInjection;
using InventoryApp.Infrastructure.Identity;
using InventoryApp.Infrastructure.Persistence;
using InventoryApp.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApp.Infrastructure;

public static class InfrastructureServiceRegistration
{
    /// <summary>
    /// Registers persistence, Identity, token issuing and startup seeding.
    /// Authentication *scheme* wiring (which is HTTP-level) lives in the API.
    /// </summary>
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName),
            JwtOptions.SectionName);

        services.AddValidatedOptions<RefreshTokenOptions>(
            configuration.GetSection(RefreshTokenOptions.SectionName),
            RefreshTokenOptions.SectionName);

        // The key itself is needed here, not just at validation time.
        var protectorOptions = services.AddValidatedOptions<RefreshTokenProtectorOptions>(
            configuration.GetSection(RefreshTokenProtectorOptions.SectionName),
            RefreshTokenProtectorOptions.SectionName);

        services.AddValidatedOptions<SeedAdminOptions>(
            configuration.GetSection(SeedAdminOptions.SectionName),
            SeedAdminOptions.SectionName);

        services.AddDatabase(configuration);
        services.AddIdentityServices();

        // TimeProvider is part of the BCL, so making it injectable costs nothing
        // and lets the tests freeze time.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenProtector>(
            new HmacRefreshTokenProtector(Encoding.UTF8.GetBytes(protectorOptions.HashingKey)));

        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var providerName = configuration["Database:Provider"] ?? DatabaseProviders.SqlServer;

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. "
                + "Set it in appsettings.json, user-secrets or the environment.");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            switch (providerName)
            {
                case DatabaseProviders.SqlServer:
                    options.UseSqlServer(
                        connectionString,
                        sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
                    break;

                case DatabaseProviders.Sqlite:
                    options.UseSqlite(connectionString);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported value '{providerName}' for Database:Provider. "
                        + $"Supported providers: {DatabaseProviders.SqlServer}, {DatabaseProviders.Sqlite}.");
            }
        });
    }

    private static void AddIdentityServices(this IServiceCollection services)
    {
        // AddIdentityCore rather than AddIdentity: the API only issues bearer
        // tokens, so none of the cookie schemes that AddIdentity registers are
        // needed, and their presence in the pipeline is a common source of
        // accidental authentication.
        services
            .AddIdentityCore<ApplicationUser>(identityOptions => ConfigureIdentity(identityOptions))
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.MaxLength = 128;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }

    private static class DatabaseProviders
    {
        public const string SqlServer = "SqlServer";
        public const string Sqlite = "Sqlite";
    }
}
