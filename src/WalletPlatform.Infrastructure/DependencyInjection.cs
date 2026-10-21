using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Infrastructure.Data;

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

        return services;
    }
}
