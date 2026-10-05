using System.Net;
using InventoryApp.Tests.Support;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace InventoryApp.Tests.Integration;

/// <summary>
/// Exercises the fixed-window limiter itself. <see cref="InventoryApiFactory"/> defaults its limits
/// high so other suites are never throttled, so these tests construct it with small limits instead.
/// </summary>
public sealed class RateLimitingTests
{
    private const int LoginPermitLimit = 3;
    private const int RefreshPermitLimit = 3;

    private const string LoginPath = "/api/auth/login";
    private const string RefreshPath = "/api/auth/refresh";

    private static readonly object BadCredentials = new { email = "absent@example.com", password = "Wrong1!Password" };

    /// <summary>
    /// Each test gets its own factory because the limiter's window state lives in the host. Sharing
    /// one host would let an earlier test exhaust the budget and make later assertions order-dependent.
    /// </summary>
    private static async Task<(HttpClient Client, InventoryApiFactory Factory)> CreateHostAsync()
    {
        var factory = new InventoryApiFactory(LoginPermitLimit, RefreshPermitLimit);

        return (factory.CreateClient(), factory);
    }

    private static async Task ExhaustLoginWindowAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < LoginPermitLimit; attempt++)
        {
            var allowed = await client.PostJsonAsync(LoginPath, BadCredentials);

            // Reaching the endpoint at all is what proves the limiter let it through.
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }
    }

    [Fact]
    public async Task Login_IsRejectedOnceTheWindowIsExhausted()
    {
        var (client, factory) = await CreateHostAsync();
        using (factory)
        {
            await ExhaustLoginWindowAsync(client);

            var rejected = await client.PostJsonAsync(LoginPath, BadCredentials);

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        }
    }

    [Fact]
    public async Task Rejection_AdvertisesWhenToRetry()
    {
        var (client, factory) = await CreateHostAsync();
        using (factory)
        {
            await ExhaustLoginWindowAsync(client);

            var rejected = await client.PostJsonAsync(LoginPath, BadCredentials);

            Assert.True(rejected.Headers.TryGetValues("Retry-After", out var values));
            Assert.True(int.TryParse(values!.Single(), out var seconds));
            Assert.InRange(seconds, 1, 60);
        }
    }

    [Fact]
    public async Task Rejection_UsesTheSameProblemDetailsContractAsEveryOtherError()
    {
        var (client, factory) = await CreateHostAsync();
        using (factory)
        {
            await ExhaustLoginWindowAsync(client);

            var rejected = await client.PostJsonAsync(LoginPath, BadCredentials);

            Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

            var problem = await rejected.ReadProblemAsync();
            Assert.Equal("Too many requests", problem.GetProperty("title").GetString());
            Assert.Equal(StatusCodes.Status429TooManyRequests, problem.GetProperty("status").GetInt32());
            Assert.True(problem.TryGetProperty("traceId", out _));
        }
    }

    [Fact]
    public async Task LoginAndRefresh_HaveSeparateBudgets()
    {
        var (client, factory) = await CreateHostAsync();
        using (factory)
        {
            await ExhaustLoginWindowAsync(client);

            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostJsonAsync(LoginPath, BadCredentials)).StatusCode);

            // A single shared limiter would have throttled this call too.
            var refresh = await client.PostJsonAsync(RefreshPath, new { refreshToken = "not-a-real-token" });

            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
    }

    [Fact]
    public async Task Refresh_IsRateLimitedOnItsOwnBudget()
    {
        var (client, factory) = await CreateHostAsync();
        using (factory)
        {
            for (var attempt = 0; attempt < RefreshPermitLimit; attempt++)
            {
                var allowed = await client.PostJsonAsync(RefreshPath, new { refreshToken = "not-a-real-token" });

                Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
            }

            var rejected = await client.PostJsonAsync(RefreshPath, new { refreshToken = "not-a-real-token" });

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        }
    }

    [Fact]
    public async Task EndpointsWithoutALimitPolicy_AreNeverThrottled()
    {
        var (client, factory) = await CreateHostAsync();
        using (factory)
        {
            // /me carries no limiter, so well beyond the login allowance these must all reach
            // authorization and come back as 401 rather than being rejected by the middleware.
            for (var attempt = 0; attempt < LoginPermitLimit + 5; attempt++)
            {
                var response = await client.GetAsync("/api/auth/me", accessToken: null);

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }
}