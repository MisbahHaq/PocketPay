using System.Reflection;
using Inventory.Application.Abstractions;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Persistence.Interceptors;
using Inventory.Infrastructure.Persistence.Repositories;
using Inventory.Infrastructure.Persistence.Seeding;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Inventory.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddApplicationServices();
        services.AddPersistence(configuration);

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Bound with ValidateOnStart so a missing or too-short signing key fails the
        // container at startup rather than at the first login attempt, where the failure
        // would look like an authentication bug.
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. Set it via user secrets, "
                + "an environment variable, or ConnectionStrings__DefaultConnection.");

        services.AddHttpContextAccessor();

        // Scoped: the audit interceptor resolves the current user per request, so the
        // context must not be shared across requests.
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<InventoryDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(InventoryDbContext).Assembly.FullName);
            });

            // Deliberately NOT calling EnableRetryOnFailure here. Npgsql's retrying
            // execution strategy rejects user-initiated transactions unless every one is
            // wrapped in strategy.ExecuteAsync(), which would forbid the manual
            // transactions that stock operations require. Retry lives in UnitOfWork
            // instead, where it can re-evaluate business rules between attempts.
            //
            // Pool size is tuned in the connection string (Maximum Pool Size) rather than
            // here, so it can be raised in production without a rebuild.

            // snake_case identifiers, matching PostgreSQL convention.
            options.UseSnakeCaseNamingConvention();

            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());

            if (provider.GetService<IHostEnvironment>()?.IsDevelopment() == true)
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<InventoryDbContext>());

        services.AddIdentityCore<ApplicationUser>(identity =>
            {
                identity.User.RequireUniqueEmail = true;
                identity.Lockout.AllowedForNewUsers = true;
                identity.Lockout.MaxFailedAccessAttempts = 5;
                identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // Sign-in with a password, not a username. Unique email plus RequireUniqueEmail
                // means one credential per human, and it removes the "which is my username?"
                // support question entirely.
                identity.SignIn.RequireConfirmedAccount = false;

                // Password rules are the authority; the FluentValidation rules for the
                // registration form mirror them so the user sees the rule before submitting.
                identity.Password.RequiredLength = 12;
                identity.Password.RequireDigit = true;
                identity.Password.RequireLowercase = true;
                identity.Password.RequireUppercase = true;
                identity.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<InventoryDbContext>()
            .AddDefaultTokenProviders();

        // No custom IUserValidator is registered on purpose: the default UserValidator<T>
        // is what enforces RequireUniqueEmail, and substituting it would quietly drop
        // duplicate-account protection.

        services.AddHealthChecks()
            .AddDbContextCheck<InventoryDbContext>(
                name: "database",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }
}