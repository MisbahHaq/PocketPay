using Microsoft.AspNetCore.HttpOverrides;

namespace WalletPlatform.Api.Configuration;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddForwardedHeadersConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Clear known networks/proxies for compatibility
        });
        return services;
    }

    public static IApplicationBuilder UseForwardedHeadersConfiguration(this IApplicationBuilder app)
    {
        app.UseForwardedHeaders();
        return app;
    }
}
