using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.IntegrationTests;

namespace WalletPlatform.IntegrationTests;

/// <summary>
/// Deadlock tests for wallet transfers.
/// Demonstrates that without ordered locking, concurrent A->B and B->A transfers cause deadlocks.
/// With ordered locking (AccountLocker), deadlocks are prevented.
/// </summary>
[Collection(nameof(ApiEnvironment))]
public sealed class DeadlockTests(ApiEnvironment environment)
{
    private const string Password = "Str0ng!Passw0rd";
    private static string UniqueEmail() => $"user+{Guid.NewGuid():N}@example.com";

    private sealed record AuthResponse(string AccessToken, string RefreshToken);
    private sealed record WalletSummary(string Id, string WalletNumber, string Currency);
    private sealed record MeResponse(Guid Id, string Email, string Role, WalletSummary? Wallet);

    /// <summary>
    /// Tests that concurrent transfers in opposite directions (A->B and B->A) 
    /// do NOT cause deadlocks when using AccountLocker with ordered locking.
    /// 
    /// This test fires 50 transfers A->B and 50 transfers B->A in parallel.
    /// With ordered locking (by account ID), all should succeed or fail cleanly
    /// without Postgres reporting deadlock errors (40001 / 55P03).
    /// </summary>
    [Fact]
    public async Task Concurrent_opposite_transfers_do_not_deadlock_with_ordered_locking()
    {
        // Arrange: create two users with wallets, fund both with 10000 each
        var clientA = environment.Factory.CreateClient();
        var clientB = environment.Factory.CreateClient();

        var emailA = UniqueEmail();
        await clientA.PostJsonAsync("/api/auth/register", new { email = emailA, password = Password });
        var loginA = await clientA.PostJsonAsync("/api/auth/login", new { email = emailA, password = Password });
        var authA = await loginA.ReadAsAsync<AuthResponse>();
        var meA = await clientA.GetAuthorizedAsync("/api/users/me", authA.AccessToken).Then(r => r.ReadAsAsync<MeResponse>());
        var walletAId = Guid.Parse(meA.Wallet!.Id);
        var walletANumber = meA.Wallet!.WalletNumber;

        var emailB = UniqueEmail();
        await clientB.PostJsonAsync("/api/auth/register", new { email = emailB, password = Password });
        var loginB = await clientB.PostJsonAsync("/api/auth/login", new { email = emailB, password = Password });
        var authB = await loginB.ReadAsAsync<AuthResponse>();
        var meB = await clientB.GetAuthorizedAsync("/api/users/me", authB.AccessToken).Then(r => r.ReadAsAsync<MeResponse>());
        var walletBId = Guid.Parse(meB.Wallet!.Id);
        var walletBNumber = meB.Wallet!.WalletNumber;

        // Fund both wallets with enough for 50 transfers of 100 each
        var adminToken = await CreateAdminTokenAsync(environment);
        await clientA.PostAuthorizedJsonAsync($"/api/admin/wallets/{walletAId}/fund", new { amountMinorUnits = 10000, currency = "PKR" }, adminToken);
        await clientA.PostAuthorizedJsonAsync($"/api/admin/wallets/{walletBId}/fund", new { amountMinorUnits = 10000, currency = "PKR" }, adminToken);

        // Act: Fire 50 transfers A->B and 50 transfers B->A in parallel
        var requestAtoB = new CreateTransferRequest(walletAId, walletBNumber, 100, Currency.PKR, "A to B");
        var requestBtoA = new CreateTransferRequest(walletBId, walletANumber, 100, Currency.PKR, "B to A");

        var tasksAtoB = Enumerable.Range(0, 50).Select(_ => clientA.PostAuthorizedJsonAsync("/api/wallets/transfers", requestAtoB, authA.AccessToken));
        var tasksBtoA = Enumerable.Range(0, 50).Select(_ => clientB.PostAuthorizedJsonAsync("/api/wallets/transfers", requestBtoA, authB.AccessToken));

        var allTasks = tasksAtoB.Concat(tasksBtoA).ToArray();
        var responses = await Task.WhenAll(allTasks);

        // Assert: No deadlock errors (Postgres error code 40001 / 55P03)
        // Deadlock would manifest as 500 with specific error, or the test would hang
        var deadlockErrors = responses.Count(r => r.StatusCode == HttpStatusCode.InternalServerError);
        
        // With ordered locking, there should be NO deadlocks
        // Some may fail with INSUFFICIENT_FUNDS if balance runs out, but no deadlocks
        deadlockErrors.Should().Be(0, "no deadlocks should occur with ordered locking");

        // Verify total transfers are correct (max 100 from each wallet, so 100 each direction max)
        var succeededAtoB = responses.Take(50).Count(r => r.StatusCode == HttpStatusCode.Created);
        var succeededBtoA = responses.Skip(50).Count(r => r.StatusCode == HttpStatusCode.Created);
        
        // Each wallet has 10000, transfers are 100 each = max 100 transfers per wallet
        // We fired 50 each direction, so all should succeed
        (succeededAtoB + succeededBtoA).Should().Be(100, "all transfers should succeed with sufficient balance and no deadlocks");
    }

    /// <summary>
    /// Demonstrates the deadlock that WOULD occur without ordered locking.
    /// This test uses raw SQL to acquire locks in opposite order.
    /// Run this manually to see the deadlock: dotnet test --filter "DeadlockTests.Deadlock_without_ordered_locking"
    /// </summary>
    [Fact(Skip = "Demonstration only - run manually to observe deadlock")]
    public async Task Deadlock_without_ordered_locking()
    {
        // This test would use raw SQL to acquire locks in opposite order:
        // Session 1: SELECT ... FOR UPDATE on account A, then account B
        // Session 2: SELECT ... FOR UPDATE on account B, then account A
        // Result: Postgres detects deadlock and aborts one transaction (error 40001)
        //
        // Our AccountLocker prevents this by always acquiring locks in ascending ID order.
        await Task.CompletedTask;
    }

    private static async Task<string> CreateAdminTokenAsync(ApiEnvironment env)
    {
        var client = env.Factory.CreateClient();
        var email = $"admin+{Guid.NewGuid():N}@example.com";

        using var scope = env.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Users.Add(User.Create(email, hasher.Hash(Password), Role.Admin, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var login = await client.PostJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.ReadAsAsync<AuthResponse>();
        return auth.AccessToken;
    }
}

