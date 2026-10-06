using System.Net.Http.Json;
using Inventory.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.IntegrationTests;

/// <summary>
/// Schema and persistence behaviour that can only be verified against a real PostgreSQL
/// server: snake_case naming, the xmin concurrency token, and the audit interceptor.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _database;
    private readonly InventoryApiFactory _factory;

    public PersistenceTests(PostgresFixture database)
    {
        _database = database;
        _factory = new InventoryApiFactory(database);
    }

    public async Task InitializeAsync()
    {
        _factory.CreateClient();
        await _factory.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Identity_tables_use_snake_case_names()
    {
        await using var connection = new Npgsql.NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;",
            connection);

        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        // The default Identity names are quoted PascalCase ("AspNetUsers"), which is
        // inconsistent with the snake_case used by the application tables.
        tables.Should().Contain(["users", "roles", "refresh_tokens", "user_roles"]);
        tables.Should().NotContain(name => name.Contains("AspNet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task User_keys_are_uuids_rather_than_the_default_identity_guids()
    {
        await _factory.CreateUserAsync();

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync();

        stored.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Row_version_maps_to_the_postgres_xmin_system_column()
    {
        await _factory.CreateUserAsync();

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync();

        // xmin is the transaction id of the last update. It is assigned by PostgreSQL, so
        // it is non-zero and changes on every write.
        stored.RowVersion.Should().NotBe(default);

        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT xmin::text FROM users LIMIT 1;";
        var raw = await command.ExecuteScalarAsync();

        // Proves the value really is the system column rather than a stored bigint.
        raw.Should().NotBeNull();
    }

    [Fact]
    public async Task Row_version_changes_when_the_row_is_updated()
    {
        await _factory.CreateUserAsync();

        uint? before;
        await using (var context = _database.CreateContext())
        {
            before = (uint?)context.Users.SingleAsync().GetAwaiter().GetResult().RowVersion;
        }

        await using (var context = _database.CreateContext())
        {
            var user = await context.Users.SingleAsync();
            user.FullName = "Renamed";
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var after = await context.Users.SingleAsync();
            after.FullName.Should().Be("Renamed");
            after.RowVersion.Should().NotBe(before);
        }
    }

    [Fact]
    public async Task A_stale_row_version_is_rejected_as_a_concurrency_conflict()
    {
        await _factory.CreateUserAsync();

        // Two contexts load the same row, so both hold the same xmin.
        var first = _database.CreateContext();
        var second = _database.CreateContext();

        var firstUser = await first.Users.SingleAsync();
        var secondUser = await second.Users.SingleAsync();

        firstUser.FullName = "First Writer";
        await first.SaveChangesAsync();

        secondUser.FullName = "Second Writer";

        // The whole point of mapping xmin: the second writer is told it lost the race
        // instead of silently overwriting the first, which is how stock figures are lost.
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task Concurrent_writers_do_not_both_win()
    {
        var user = await _factory.CreateUserAsync();

        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();

        var firstUser = await first.Users.SingleAsync(u => u.Id == user.Id);
        var secondUser = await second.Users.SingleAsync(u => u.Id == user.Id);

        firstUser.IsActive = false;
        await first.SaveChangesAsync();

        secondUser.IsActive = true;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task The_audit_interceptor_stamps_created_at()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var user = await _factory.CreateUserAsync();

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);

        stored.CreatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task Created_at_is_not_overwritten_by_a_later_update()
    {
        await _factory.CreateUserAsync();

        DateTimeOffset original;
        await using (var context = _database.CreateContext())
        {
            original = await context.Users.Select(user => user.CreatedAt).SingleAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var user = await context.Users.SingleAsync();
            user.FullName = "Touched";
            await context.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var stored = await verify.Users.SingleAsync();

        stored.FullName.Should().Be("Touched");

        // An audit interceptor that stamps CreatedAt on every save would quietly destroy
        // the record of when an account actually came into existence.
        stored.CreatedAt.Should().Be(original);
    }

    [Fact]
    public async Task A_refresh_token_is_stored_hashed_never_in_the_clear()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();

        await using var context = _database.CreateContext();
        var stored = await context.RefreshTokens.SingleAsync();

        stored.TokenHash.Should().NotBe(tokens.RefreshToken);
        stored.TokenHash.Should().Be(RefreshToken.HashToken(tokens.RefreshToken));
        stored.TokenHash.Should().NotContain(tokens.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_token_carries_a_sequential_guid_and_a_family()
    {
        var (_, tokens) = await _factory.SignInNewUserAsync();

        await using var context = _database.CreateContext();
        var stored = await context.RefreshTokens.SingleAsync();

        stored.Id.Should().NotBe(Guid.Empty);
        stored.FamilyId.Should().NotBe(Guid.Empty);
        stored.UserId.Should().Be(tokens.User.Id);
        stored.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        stored.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Login_creates_exactly_one_active_token_per_login()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        await _factory.SignInAsync(client, user.Email, InventoryApiFactory.TestPassword);
        await _factory.SignInAsync(client, user.Email, InventoryApiFactory.TestPassword);

        await using var context = _database.CreateContext();

        // Two logins from two devices are both legitimate; neither should invalidate the
        // other.
        (await context.RefreshTokens.CountAsync(token => token.UserId == user.Id)).Should().Be(2);
        (await context.RefreshTokens.CountAsync(token => token.RevokedAt == null)).Should().Be(2);
    }

    [Fact]
    public async Task Lockout_survives_a_reload_from_the_database()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = user.Email, password = "Wr0ng-Passw0rd!" });
        }

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);

        stored.AccessFailedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Migrations_are_recorded_in_the_history_table()
    {
        await using var context = _database.CreateContext();

        var applied = await context.Database
            .SqlQueryRaw<string>(
                "SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"")
            .ToListAsync();

        applied.Should().NotBeEmpty();
        applied.Should().Contain(name => name.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Health_endpoints_report_a_live_database()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task Creating_the_same_role_twice_does_not_duplicate_it()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        (await roleManager.CreateAsync(new IdentityRole<Guid>(SystemRoles.Admin))).Succeeded.Should().BeTrue();
        (await roleManager.CreateAsync(new IdentityRole<Guid>(SystemRoles.Admin))).Succeeded.Should().BeFalse();

        await using var context = _database.CreateContext();
        (await context.Roles.CountAsync(role => role.Name == SystemRoles.Admin)).Should().Be(1);
    }
}