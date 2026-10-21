using System.Net;
using FluentAssertions;

namespace WalletPlatform.IntegrationTests;

[Collection(nameof(ApiEnvironment))]
public sealed class HealthEndpointTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Ready_endpoint_returns_ok_when_database_is_reachable()
    {
        var client = environment.Factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
