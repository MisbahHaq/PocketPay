using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Application.Admin.GetUsersPage;
using WalletPlatform.Application.Auth.Login;
using WalletPlatform.Application.Auth.Refresh;
using WalletPlatform.Application.Auth.RegisterUser;
using WalletPlatform.Application.Users.GetCurrentUser;

namespace WalletPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterUserRequestValidator>(includeInternalTypes: true);

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<GetUsersPageHandler>();

        return services;
    }
}
