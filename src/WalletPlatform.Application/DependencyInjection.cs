using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Application.Admin.FundWallet;
using WalletPlatform.Application.Admin.GetTrialBalance;
using WalletPlatform.Application.Admin.GetUsersPage;
using WalletPlatform.Application.Admin.ReverseJournalEntry;
using WalletPlatform.Application.Auth.Login;
using WalletPlatform.Application.Auth.Refresh;
using WalletPlatform.Application.Auth.RegisterUser;
using WalletPlatform.Application.Ledger.GetWalletBalance;
using WalletPlatform.Application.Ledger.GetWalletStatement;
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
        services.AddScoped<GetWalletBalanceHandler>();
        services.AddScoped<GetWalletStatementHandler>();
        services.AddScoped<FundWalletHandler>();
        services.AddScoped<ReverseJournalEntryHandler>();
        services.AddScoped<GetTrialBalanceHandler>();

        return services;
    }
}
