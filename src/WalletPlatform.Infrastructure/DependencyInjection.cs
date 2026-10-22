using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Security;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Persistence;
using WalletPlatform.Infrastructure.Security;

namespace WalletPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PayPocketDb")
            ?? throw new InvalidOperationException("Connection string 'PayPocketDb' is missing.");

        services.AddDbContext<WalletDbContext>(options => options.UseNpgsql(connectionString));

        services.AddHealthChecks()
            .AddDbContextCheck<WalletDbContext>(name: "postgresql", tags: ["ready"]);

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddOptions<AdminSeedOptions>()
            .BindConfiguration(AdminSeedOptions.SectionName);
        services.AddOptions<SeedOptions>()
            .BindConfiguration(SeedOptions.SectionName);
        services.AddScoped<AdminSeeder>();
        services.AddScoped<SystemAccountSeeder>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IWalletNumberGenerator, SecureWalletNumberGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ILedgerAccountRepository, LedgerAccountRepository>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
