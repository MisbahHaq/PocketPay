using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.IntegrationTests;

public sealed class ApiEnvironment : IAsyncLifetime
{
    private const string TestSigningKey = "integration-test-signing-key-at-least-32-chars-long";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:PayPocketDb"] = _postgres.GetConnectionString(),
                        ["Jwt:Issuer"] = "paypocket-integration-tests",
                        ["Jwt:Audience"] = "paypocket-integration-tests",
                        ["Jwt:SigningKey"] = TestSigningKey,
                        ["Jwt:AccessTokenMinutes"] = "15",
                        ["Jwt:RefreshTokenDays"] = "7",
                    });
                });
            });

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>Creates a DI scope for resolving scoped services (e.g. the DbContext).</summary>
    public IServiceScope CreateScope() =>
        Factory.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
}

[CollectionDefinition(nameof(ApiEnvironment))]
public sealed class ApiCollection : ICollectionFixture<ApiEnvironment>
{
}
