using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Concurrency and idempotency tests for wallet transfers.
/// These tests are expected to FAIL before Week 5 fixes are implemented.
/// They demonstrate:
/// 1. TOCTOU race condition: two concurrent transfers from same wallet can both pass balance check
/// 2. Missing idempotency: duplicate requests with same payload create multiple transfers
/// </summary>
[Collection(nameof(ApiEnvironment))]
public sealed class TransferConcurrencyTests(ApiEnvironment environment)
{
    private const string Password = "Str0ng!Passw0rd";
    private static string UniqueEmail() => $"user+{Guid.NewGuid():N}@example.com";

    private sealed record AuthResponse(string AccessToken, string RefreshToken);
    private sealed record WalletSummary(string Id, string WalletNumber, string Currency);
    private sealed record MeResponse(Guid Id, string Email, string Role, WalletSummary? Wallet);
    private sealed record TransferResponse(Guid Id, Guid SourceWalletId, Guid DestinationWalletId, long AmountMinorUnits, long FeeMinorUnits, string Currency, string Status, string? Note, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? FailureReason);

    /// <summary>
    /// TOCTOU race condition: two concurrent transfers from the same wallet, each for 600,
    /// when the wallet only has 1000. Both pass the balance check before either commits,
    /// resulting in an overdraft (balance goes negative).
    /// 
    /// Expected to FAIL before Week 5 fix (pessimistic locking).
    /// </summary>
    [Fact]
    public async Task Two_concurrent_transfers_from_same_wallet_do_not_overdraw()
    {
        // Arrange: register two users, fund source wallet with 1000
        var client1 = environment.Factory.CreateClient();
        var client2 = environment.Factory.CreateClient();

        // Register user1 (source)
        var email1 = UniqueEmail();
        await client1.PostJsonAsync("/api/auth/register", new { email = email1, password = Password });
        var login1 = await client1.PostJsonAsync("/api/auth/login", new { email = email1, password = Password });
        var auth1 = await login1.ReadAsAsync<AuthResponse>();

        var me1 = await client1.GetAuthorizedAsync("/api/users/me", auth1.AccessToken).Then(r => r.ReadAsAsync<MeResponse>());
        var wallet1Id = Guid.Parse(me1.Wallet!.Id);

        // Register user2 (dest)
        var email2 = UniqueEmail();
        await client2.PostJsonAsync("/api/auth/register", new { email = email2, password = Password });
        var login2 = await client2.PostJsonAsync("/api/auth/login", new { email = email2, password = Password });
        var auth2 = await login2.ReadAsAsync<AuthResponse>();

        var me2 = await client2.GetAuthorizedAsync("/api/users/me", auth2.AccessToken).Then(r => r.ReadAsAsync<MeResponse>());
        var wallet2Number = me2.Wallet!.WalletNumber;

        // Fund wallet1 with 1000 minor units using admin
        var adminToken = await CreateAdminTokenAsync(environment);
        var fund = await client1.PostAuthorizedJsonAsync($"/api/admin/wallets/{wallet1Id}/fund", new { amountMinorUnits = 1000, currency = "PKR" }, adminToken);
        fund.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: fire two concurrent transfers of 600 each (total 1200 > 1000 balance)
        var transferRequest = new CreateTransferRequest(wallet1Id, wallet2Number, 600, Currency.PKR, "concurrent test");

        var task1 = client1.PostAuthorizedJsonAsync("/api/wallets/transfers", transferRequest, auth1.AccessToken);
        var task2 = client1.PostAuthorizedJsonAsync("/api/wallets/transfers", transferRequest, auth1.AccessToken);

        var responses = await Task.WhenAll(task1, task2);

        // Assert: at most one should succeed (Completed), the other should fail (InsufficientFunds)
        // BEFORE FIX: both may succeed, causing overdraft
        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var failed = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        // This assertion documents the expected correct behavior
        succeeded.Should().BeLessOrEqualTo(1, "only one transfer should succeed due to balance");
        failed.Should().BeGreaterOrEqualTo(1, "at least one should fail with INSUFFICIENT_FUNDS");
    }

    /// <summary>
    /// Missing idempotency: same payload sent 10 times in parallel creates multiple transfers.
    /// 
    /// Expected to FAIL before Week 5 fix (idempotency keys).
    /// </summary>
    [Fact]
    public async Task Duplicate_requests_without_idempotency_key_create_multiple_transfers()
    {
        var client = environment.Factory.CreateClient();

        // Register user
        var email = UniqueEmail();
        await client.PostJsonAsync("/api/auth/register", new { email, password = Password });
        var login = await client.PostJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.ReadAsAsync<AuthResponse>();

        var me = await client.GetAuthorizedAsync("/api/users/me", auth.AccessToken).Then(r => r.ReadAsAsync<MeResponse>());
        var walletId = Guid.Parse(me.Wallet!.Id);

        // Register another user as destination
        var email2 = UniqueEmail();
        var client2 = environment.Factory.CreateClient();
        await client2.PostJsonAsync("/api/auth/register", new { email = email2, password = Password });
        var login2 = await client2.PostJsonAsync("/api/auth/login", new { email = email2, password = Password });
        var auth2 = await login2.ReadAsAsync<AuthResponse>();
        var me2 = await client2.GetAuthorizedAsync("/api/users/me", auth2.AccessToken).Then(r => r.ReadAsAsync<MeResponse>());
        var wallet2Number = me2.Wallet!.WalletNumber;

        // Fund source wallet with enough for multiple transfers
        var adminToken = await CreateAdminTokenAsync(environment);
        await client.PostAuthorizedJsonAsync($"/api/admin/wallets/{walletId}/fund", new { amountMinorUnits = 10000, currency = "PKR" }, adminToken);

        // Act: send 10 identical requests in parallel (no idempotency key)
        var transferRequest = new CreateTransferRequest(walletId, wallet2Number, 100, Currency.PKR, "duplicate test");
        var tasks = Enumerable.Range(0, 10).Select(_ => client.PostAuthorizedJsonAsync("/api/wallets/transfers", transferRequest, auth.AccessToken));
        var responses = await Task.WhenAll(tasks);

        // Count successes
        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);

        // BEFORE FIX: all 10 may succeed (creating 10 transfers for 1000 total)
        // AFTER FIX: only 1 should succeed (idempotency prevents duplicates)
        succeeded.Should().Be(1, "only one transfer should be created for duplicate requests");
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

internal static class TaskExtensions
{
    public static async Task<TResult> Then<T, TResult>(this Task<T> task, Func<T, Task<TResult>> continuation)
    {
        var result = await task;
        return await continuation(result);
    }
}