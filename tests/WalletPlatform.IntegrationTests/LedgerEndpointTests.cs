using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class LedgerEndpointTests(ApiEnvironment environment)
{
    private const string Password = "Str0ng!Passw0rd";

    private sealed record AuthResponse(string AccessToken, string RefreshToken);
    private sealed record WalletResponse(string Id, string WalletNumber, string Currency);
    private sealed record MeResponse(Guid Id, string Email, string Role, WalletResponse? Wallet);
    private sealed record BalanceResponse(Guid LedgerAccountId, Guid? WalletId, string Currency, string AccountType, long SignedMinorUnits);
    private sealed record StatementLine(string JournalEntryId, DateTimeOffset CreatedAtUtc, string Direction, long AmountMinorUnits, long RunningBalanceMinorUnits, string? Type, string? Reference, string? Description);
    private sealed record StatementResponse(Guid LedgerAccountId, Guid? WalletId, string Currency, long ClosingBalanceMinorUnits, int Page, int PageSize, int TotalCount, List<StatementLine> Lines);
    private sealed record FundWalletResponse(Guid JournalEntryId, Guid WalletId, long AmountMinorUnits, string Currency);
    private sealed record TrialBalanceCurrencyLine(string Currency, long TotalDebits, long TotalCredits, long Difference);
    private sealed record TrialBalanceResponse(bool IsBalanced, List<TrialBalanceCurrencyLine> Currencies);

    private async Task<(HttpClient Client, string Token, Guid WalletId)> RegisterCustomerAsync()
    {
        var client = environment.Factory.CreateClient();
        var email = $"user+{Guid.NewGuid():N}@example.com";

        await client.PostJsonAsync("/api/auth/register", new { email, password = Password });
        var login = await client.PostJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.ReadAsAsync<AuthResponse>();

        var me = await client.GetAuthorizedAsync("/api/users/me", auth.AccessToken);
        var profile = await me.ReadAsAsync<MeResponse>();

        return (client, auth.AccessToken, Guid.Parse(profile.Wallet!.Id));
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        var client = environment.Factory.CreateClient();
        var email = $"admin+{Guid.NewGuid():N}@example.com";

        using var scope = environment.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Users.Add(User.Create(email, hasher.Hash(Password), Role.Admin, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var login = await client.PostJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.ReadAsAsync<AuthResponse>();
        return auth.AccessToken;
    }

    [Fact]
    public async Task Funded_wallet_reports_balance_and_statement_to_its_owner()
    {
        var (client, token, walletId) = await RegisterCustomerAsync();
        var adminToken = await CreateAdminTokenAsync();

        var fund = await client.PostAuthorizedJsonAsync(
            $"/api/admin/wallets/{walletId}/fund",
            new { amountMinorUnits = 250_000 },
            adminToken);
        fund.StatusCode.Should().Be(HttpStatusCode.Created);

        var balanceResponse = await client.GetAuthorizedAsync(
            $"/api/wallets/{walletId}/balance", token);
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var balance = await balanceResponse.ReadAsAsync<BalanceResponse>();
        balance.SignedMinorUnits.Should().Be(250_000);
        balance.AccountType.Should().Be("Liability");

        var statementResponse = await client.GetAuthorizedAsync(
            $"/api/wallets/{walletId}/statement", token);
        statementResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statement = await statementResponse.ReadAsAsync<StatementResponse>();
        statement.TotalCount.Should().Be(1);
        statement.ClosingBalanceMinorUnits.Should().Be(250_000);
        statement.Lines.Should().ContainSingle();
        statement.Lines[0].Direction.Should().Be("Credit");
        statement.Lines[0].RunningBalanceMinorUnits.Should().Be(250_000);
    }

    [Fact]
    public async Task Another_users_wallet_balance_returns_not_found()
    {
        var (_, _, firstWalletId) = await RegisterCustomerAsync();
        var (thirdClient, thirdToken, _) = await RegisterCustomerAsync();

        var response = await thirdClient.GetAuthorizedAsync(
            $"/api/wallets/{firstWalletId}/balance", thirdToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_cannot_fund_a_wallet()
    {
        var (client, token, walletId) = await RegisterCustomerAsync();

        var response = await client.PostAuthorizedJsonAsync(
            $"/api/admin/wallets/{walletId}/fund",
            new { amountMinorUnits = 1000 },
            token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_cannot_read_a_balance()
    {
        var (_, _, walletId) = await RegisterCustomerAsync();

        var response = await environment.Factory.CreateClient()
            .GetAsync($"/api/wallets/{walletId}/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Funding_with_non_positive_amount_returns_validation_problem()
    {
        var (client, _, walletId) = await RegisterCustomerAsync();
        var adminToken = await CreateAdminTokenAsync();

        var response = await client.PostAuthorizedJsonAsync(
            $"/api/admin/wallets/{walletId}/fund",
            new { amountMinorUnits = 0 },
            adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Funding_unknown_wallet_returns_not_found()
    {
        var (client, _, _) = await RegisterCustomerAsync();
        var adminToken = await CreateAdminTokenAsync();

        var response = await client.PostAuthorizedJsonAsync(
            $"/api/admin/wallets/{Guid.NewGuid()}/fund",
            new { amountMinorUnits = 1000 },
            adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Trial_balance_is_balanced_after_funding()
    {
        var (client, _, walletId) = await RegisterCustomerAsync();
        var adminToken = await CreateAdminTokenAsync();

        await client.PostAuthorizedJsonAsync(
            $"/api/admin/wallets/{walletId}/fund",
            new { amountMinorUnits = 123_456 },
            adminToken);

        var response = await client.GetAuthorizedAsync("/api/admin/ledger/trial-balance", adminToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trial = await response.ReadAsAsync<TrialBalanceResponse>();

        trial.IsBalanced.Should().BeTrue();
        trial.Currencies.Should().Contain(line => line.Currency == "PKR")
            .Which.Difference.Should().Be(0);
    }

    [Fact]
    public async Task Admin_can_reverse_a_funding_entry_and_balance_returns_to_zero()
    {
        var (client, token, walletId) = await RegisterCustomerAsync();
        var adminToken = await CreateAdminTokenAsync();

        var fund = await client.PostAuthorizedJsonAsync(
            $"/api/admin/wallets/{walletId}/fund",
            new { amountMinorUnits = 100_000 },
            adminToken);
        var funded = await fund.ReadAsAsync<FundWalletResponse>();

        var reverse = await client.PostAuthorizedJsonAsync(
            $"/api/admin/journal-entries/{funded.JournalEntryId}/reverse",
            new { description = "mistake" },
            adminToken);
        reverse.StatusCode.Should().Be(HttpStatusCode.Created);

        var balance = await (await client.GetAuthorizedAsync(
            $"/api/wallets/{walletId}/balance", token)).ReadAsAsync<BalanceResponse>();
        balance.SignedMinorUnits.Should().Be(0);

        var reverseAgain = await client.PostAuthorizedJsonAsync(
            $"/api/admin/journal-entries/{funded.JournalEntryId}/reverse",
            new { description = (string?)null },
            adminToken);
        reverseAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
