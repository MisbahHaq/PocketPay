using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WalletPlatform.Api.Configuration;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddConfiguredForwardedHeaders(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Known proxies configured via ForwardedHeaders:KnownProxies list
        });
        return services;
    }
}
