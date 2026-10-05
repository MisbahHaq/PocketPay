using FluentValidation;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common.Options;
using InventoryApp.Application.Services;
using InventoryApp.Application.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApp.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the use-case layer: services, validators and the options those services depend on.
    /// Everything here is provider-agnostic, so this method is equally valid in a test host.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtTokenOptions>()
            .Bind(configuration.GetSection(JwtTokenOptions.SectionName))
            .Validate(
                options => options.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters; HMAC-SHA256 requires a 256-bit key.")
            .Validate(
                options => !options.SigningKey.Contains("replace-me", StringComparison.OrdinalIgnoreCase),
                "Jwt:SigningKey is still the placeholder from appsettings.json. Set a real secret with: "
                + "dotnet user-secrets set \"Jwt:SigningKey\" \"<32+ character secret>\"")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than zero.")
            .Validate(options => options.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName));

        // Injected instead of calling DateTimeOffset.UtcNow directly so token expiry is testable.
        services.TryAddSingletonTimeProvider();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
