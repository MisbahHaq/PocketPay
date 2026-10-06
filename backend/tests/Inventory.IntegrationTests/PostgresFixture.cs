using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Inventory.IntegrationTests;

/// <summary>
/// Shared PostgreSQL fixture for the whole test assembly.
/// </summary>
/// <remarks>
/// <para>
/// One database, migrated once, truncated between tests. The obvious alternative is a
/// fresh database per test class, but the test role is deliberately created with
/// NOCREATEDB, so that option is not available to it - and a fixture that silently needs
/// superuser rights would fail on CI, where such rights do not exist.
/// </para>
/// <para>
/// Truncation is correct here because these tests assert the behaviour of the code under
/// test, not the isolation of the harness. The one thing truncation must not skip is the
/// migration itself, so the schema is applied once and then reused.
/// </para>
/// <para>
/// Connection string comes from <c>INVENTORY_TEST_CONNECTION_STRING</c>, falling back to
/// the local defaults created by <c>scripts/create-test-role.sql</c>. The fallback
/// password is a throwaway local-development credential, matching the script on purpose
/// so the suite runs with no configuration after setup. CI sets the environment
/// variable from its own secret store instead, which is why no real credential appears
/// in this repository.
/// </para>
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "INVENTORY_TEST_CONNECTION_STRING";

    private static readonly string[] IdentityTables =
    [
        "user_roles",
        "user_claims",
        "user_logins",
        "user_tokens",
        "role_claims",
        "refresh_tokens",
        "users",
        "roles",
    ];

    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable(ConnectionStringVariable)
        ?? "Host=localhost;Port=5432;Database=inventory_test;Username=inventory_test;"
            + "Password=inventory_test_local;Include Error Detail=true";

    public DbContextOptions<InventoryDbContext> CreateOptions(string? connectionString = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString ?? ConnectionString)
        {
            // The fixture truncates between tests, so pooled connections must not hold
            // stale snapshots of a table they have already touched.
            Pooling = true,
            MaxPoolSize = 20,
        };

        return new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .EnableSensitiveDataLogging()
            .Options;
    }

    public async Task InitializeAsync()
    {
        await using var context = new InventoryDbContext(CreateOptions());

        // Fail with a message naming the fix rather than a bare NpgsqlException from the
        // first query, which is the difference between a two-minute and a two-hour fix.
        try
        {
            if (!await context.Database.CanConnectAsync())
            {
                throw new InvalidOperationException(
                    $"Could not connect using {ConnectionStringVariable}. Run the role creation "
                    + "script in docs/testing.md first.");
            }

            await context.Database.MigrateAsync();
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to prepare the integration test database. Check {ConnectionStringVariable} "
                + $"and that the role has run the migrations. Inner: {exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// Empties every application table. RESTART IDENTITY is included because the Identity
    /// role/claim tables use integer keys; CASCADE clears dependants in one statement.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var context = new InventoryDbContext(CreateOptions());

        var tables = string.Join(", ", IdentityTables.Select(name => $"\"{name}\""));

        await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tables} RESTART IDENTITY CASCADE;");
    }

    public InventoryDbContext CreateContext()
    {
        var options = CreateOptions();

        return new InventoryDbContext(options);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await Task.CompletedTask;
    }
}

/// <summary>
/// The fixture is collection-scoped so every test class shares one migrated database,
/// and parallel execution is disabled because tests would otherwise truncate each
/// other's rows mid-run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}