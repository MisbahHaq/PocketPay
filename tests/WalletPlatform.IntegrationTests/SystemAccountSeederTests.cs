using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Persistence;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class SystemAccountSeederTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Seeding_twice_creates_one_account_per_code_and_currency()
    {
        using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var seeder = new SystemAccountSeeder(
            scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>(),
            unitOfWork,
            scope.ServiceProvider.GetRequiredService<WalletPlatform.Application.Abstractions.Security.IClock>(),
            NullLogger<SystemAccountSeeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var systemAccounts = await dbContext.LedgerAccounts
            .Where(account => account.SystemCode != null)
            .ToListAsync();

        systemAccounts.Should().HaveCount(
            Enum.GetValues<Currency>().Length * Enum.GetValues<SystemAccount>().Length);
        systemAccounts.Select(account => (account.SystemCode, account.Currency))
            .Should().OnlyHaveUniqueItems();
    }
}
