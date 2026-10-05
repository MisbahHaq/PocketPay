using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Security;
using InventoryApp.Tests.Support;

namespace InventoryApp.Tests.Integration;

/// <summary>
/// Proves each authorization policy resolves to the intended roles, using the sample endpoints.
/// 401 means no usable token; 403 means a valid token without the required role.
/// </summary>
public class AuthorizationTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private static readonly CancellationToken None = CancellationToken.None;

    private const string ManageUsersPath = "/api/samples/manage-users";
    private const string ApproveAdjustmentsPath = "/api/samples/approve-adjustments";
    private const string RecordMovementsPath = "/api/samples/record-movements";

    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string, string, HttpStatusCode> PolicyMatrix => new()
    {
        { SystemRoles.Admin, ManageUsersPath, HttpStatusCode.OK },
        { SystemRoles.Admin, ApproveAdjustmentsPath, HttpStatusCode.OK },
        { SystemRoles.Admin, RecordMovementsPath, HttpStatusCode.OK },
        { SystemRoles.Manager, ManageUsersPath, HttpStatusCode.Forbidden },
        { SystemRoles.Manager, ApproveAdjustmentsPath, HttpStatusCode.OK },
        { SystemRoles.Manager, RecordMovementsPath, HttpStatusCode.OK },
        { SystemRoles.Staff, ManageUsersPath, HttpStatusCode.Forbidden },
        { SystemRoles.Staff, ApproveAdjustmentsPath, HttpStatusCode.Forbidden },
        { SystemRoles.Staff, RecordMovementsPath, HttpStatusCode.OK }
    };

    [Theory]
    [MemberData(nameof(PolicyMatrix))]
    public async Task EachPolicy_AllowsExactlyTheIntendedRoles(string role, string path, HttpStatusCode expected)
    {
        var tokens = await _client.RegisterAndLoginAsync(role);

        var response = await _client.GetAsync(path, tokens.AccessToken, None);

        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.IsProblemDetails());
    }

    [Theory]
    [InlineData(ManageUsersPath)]
    [InlineData(ApproveAdjustmentsPath)]
    [InlineData(RecordMovementsPath)]
    public async Task ProtectedEndpoints_RejectAnAbsentToken(string path)
    {
        var response = await _client.GetAsync(path, accessToken: null, None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemDetails());
        Assert.NotEmpty(response.Headers.WwwAuthenticate);
    }

    [Theory]
    [InlineData(ManageUsersPath)]
    [InlineData(ApproveAdjustmentsPath)]
    [InlineData(RecordMovementsPath)]
    public async Task ProtectedEndpoints_RejectATamperedToken(string path)
    {
        var tokens = await _client.RegisterAndLoginAsync(SystemRoles.Admin);
        var tampered = TamperWithSignature(tokens.AccessToken);

        var response = await _client.GetAsync(path, tampered, None);

        // The signature check is what stops a caller from simply adding "Admin" to their own token.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_ReportsTheCallersRolesWithoutLeakingOtherDetail()
    {
        var tokens = await _client.RegisterAndLoginAsync(SystemRoles.Staff);

        var response = await _client.GetAsync(ManageUsersPath, tokens.AccessToken, None);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden", problem.GetProperty("title").GetString());
        Assert.NotNull(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task SampleEndpoint_EchoesTheCallersClaimsFromTheToken()
    {
        var tokens = await _client.RegisterAndLoginAsync(SystemRoles.Staff);

        var response = await _client.GetAsync(RecordMovementsPath, tokens.AccessToken, None);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var roles = body.GetProperty("Roles").EnumerateArray().Select(role => role.GetString()).ToArray();
        Assert.Equal([SystemRoles.Staff], roles);
        Assert.Equal(SystemRoles.Staff, body.GetProperty("Policy").GetString());
    }

    /// <summary>
    /// Flips the final character of the signature, leaving a structurally valid JWT whose
    /// signature no longer matches the payload.
    /// </summary>
    private static string TamperWithSignature(string accessToken)
    {
        var lastCharacter = accessToken[^1];
        var replacement = lastCharacter == 'A' ? 'B' : 'A';
        return string.Concat(accessToken.AsSpan(0, accessToken.Length - 1), replacement.ToString());
    }
}