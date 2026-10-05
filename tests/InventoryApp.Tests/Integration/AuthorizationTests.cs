using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApp.Application.Common;
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

        // Errors carry the same ProblemDetails contract as every other failure in the API.
        Assert.Equal(expected != HttpStatusCode.OK, response.IsProblemDetails());
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
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task SampleEndpoint_EchoesTheCallersClaimsFromTheToken()
    {
        var tokens = await _client.RegisterAndLoginAsync(SystemRoles.Staff);

        var response = await _client.GetAsync(RecordMovementsPath, tokens.AccessToken, None);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Responses are camelCased, so claims are read under their JSON names.
        var roles = body.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!).ToArray();
        Assert.Equal([SystemRoles.Staff], roles);

        // The endpoint echoes back which policy granted access, which is what was authorized.
        Assert.Equal(AuthorizationPolicies.CanRecordMovements, body.GetProperty("policy").GetString());
    }

    /// <summary>
    /// Flips the final character of the signature, leaving a structurally valid JWT whose
    /// signature no longer matches the payload.
    /// </summary>
    /// <summary>
    /// Flips one character of the signature segment.
    /// </summary>
    /// <remarks>
    /// Deliberately not the last character. Base64url encodes 6 bits per character, and the final
    /// character of a 32-byte signature has only 4 significant bits - the other 2 are padding that
    /// the decoder discards. Rewriting it can therefore produce a different *string* that decodes to
    /// the identical *signature*, which is a token that is still valid. Flipping a character in the
    /// middle always changes the decoded bytes, so the test fails when the check is broken instead
    /// of passing or failing depending on the random token it was handed.
    /// </remarks>
    private static string TamperWithSignature(string accessToken)
    {
        var parts = accessToken.Split('.');

        // Index 1 is the signature; 30 sits well inside it. Index 0 is the header, which is
        // base64 of a fixed prefix, so tampering there tests header parsing rather than signing.
        const int IndexToCorrupt = 30;

        var signature = parts[1].ToCharArray();
        signature[IndexToCorrupt] = signature[IndexToCorrupt] == 'A' ? 'B' : 'A';
        parts[1] = new string(signature);

        return string.Join('.', parts);
    }
}