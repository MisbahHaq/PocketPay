using FluentValidation;
using InventoryApp.Application.Auth;
using InventoryApp.Application.Auth.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApp.Application;

public static class ApplicationServiceRegistration
{
    /// <summary>
    /// Registers the application layer: request validators and use-case services.
    /// No transport or persistence concerns belong here.
    /// </summary>
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterUserRequestValidator>(ServiceLifetime.Singleton);

        // AuthService is scoped because it depends on the scoped UserManager and
        // DbContext.
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
