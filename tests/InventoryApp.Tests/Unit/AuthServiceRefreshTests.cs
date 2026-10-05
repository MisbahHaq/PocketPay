using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Common.Options;
using InventoryApp.Application.Dtos;
using InventoryApp.Application.Security;
using InventoryApp.Application.Services;
using InventoryApp.Application.Validators;
using InventoryApp.Domain.Entities;
using InventoryApp.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryApp.Tests.Unit;

/// <summary>
/// Unit tests for the refresh token rotation rules. Storage, clock and token minting are all
/// replaced, so each test states one rule about rotation and nothing else.
/// </summary>
public class AuthServiceRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestRefreshTokenRepository _refreshTokens = new();
    private readonly TestUserManagerHarness _users = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly Mock<ITokenService> _tokenService = new();

    public AuthServiceRefreshTests()
    {
        _tokenService
            .Setup(service => service.CreateAccessToken(It.IsAny<ApplicationUser>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns((ApplicationUser user, IReadOnlyCollection<string> roles) =>
                new AccessTokenResult($"access-token-for-{user.Id}", Now.AddMinutes(15)));
    }

    [Fact]
    public async Task RefreshAsync_RotatesTheTokenAndLinksTheSuccessor()
    {
        var user = _users.AddUser("Str0ng!Password1", roles: "Staff");
        var (presentedValue, presentedHash) = RefreshTokenGenerator.Create();
        var stored = AddToken(user, presentedHash);

        var response = await Service().RefreshAsync(new RefreshTokenRequest(presentedValue), CancellationToken.None);

        Assert.NotEqual(presentedValue, response.RefreshToken);
        Assert.Equal(Now, stored.RevokedAt);
        Assert.Equal("rotated", stored.RevokedReason);
        Assert.Equal(RefreshTokenGenerator.Hash(response.RefreshToken), stored.ReplacedByTokenHash);

        var successor = Assert.Single(
            _refreshTokens.Tokens,
            token => token.TokenHash == RefreshTokenGenerator.Hash(response.RefreshToken));

        Assert.Equal(stored.FamilyId, successor.FamilyId);
        Assert.Equal(user.Id, successor.UserId);
        Assert.Equal(Now.AddDays(7), successor.ExpiresAt);

        // Rotation must be one commit: retiring the old token and storing its successor.
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RefreshAsync_RevokesTheWholeFamilyWhenARotatedTokenIsReplayed()
    {
        var user = _users.AddUser("Str0ng!Password1");
        var familyId = Guid.NewGuid();
        var (stolenValue, stolenHash) = RefreshTokenGenerator.Create();

        // The stolen token was already rotated once; a second presentation is a replay.
        var stolen = AddToken(user, stolenHash, familyId, revokedReason: "rotated");
        stolen.ReplacedByTokenHash = RefreshTokenGenerator.Hash("the-legitimate-successor");

        // The token the legitimate client is currently holding, same login family.
        var (_, liveHash) = RefreshTokenGenerator.Create();
        AddToken(user, liveHash, familyId);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().RefreshAsync(new RefreshTokenRequest(stolenValue), CancellationToken.None));

        Assert.Equal("The refresh token is invalid or has expired.", exception.Message);
        Assert.NotNull(liveHash);

        var live = _refreshTokens.Tokens.Single(token => token.TokenHash == liveHash);
        Assert.Equal(Now, live.RevokedAt);
        Assert.Equal("reused-after-rotation", live.RevokedReason);

        // Nothing new was minted: a replay must not hand the caller another token.
        Assert.Equal(2, _refreshTokens.Tokens.Count);
    }

    [Fact]
    public async Task RefreshAsync_TreatsARevokedButUnrotatedTokenAsExpiredNotAsReuse()
    {
        var user = _users.AddUser("Str0ng!Password1");
        var familyId = Guid.NewGuid();
        var (loggedOutValue, loggedOutHash) = RefreshTokenGenerator.Create();

        // Revoked by logout: no successor, so this is not evidence of theft.
        AddToken(user, loggedOutHash, familyId, revokedReason: "logout");

        var (_, liveHash) = RefreshTokenGenerator.Create();
        AddToken(user, liveHash, familyId);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().RefreshAsync(new RefreshTokenRequest(loggedOutValue), CancellationToken.None));

        // A logout must not nuke every other device signed in under the same account.
        var live = _refreshTokens.Tokens.Single(token => token.TokenHash == liveHash);
        Assert.Null(live.RevokedAt);
        Assert.Equal(2, _refreshTokens.Tokens.Count);
    }

    [Fact]
    public async Task RefreshAsync_RejectsAnExpiredToken()
    {
        var user = _users.AddUser("Str0ng!Password1");
        var (value, hash) = RefreshTokenGenerator.Create();
        AddToken(user, hash, expiresAt: Now.AddDays(-1));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().RefreshAsync(new RefreshTokenRequest(value), CancellationToken.None));

        Assert.Single(_refreshTokens.Tokens);
        Assert.Null(_refreshTokens.Tokens[0].RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_RejectsAnUnknownToken()
    {
        _users.AddUser("Str0ng!Password1");
        var (value, _) = RefreshTokenGenerator.Create();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().RefreshAsync(new RefreshTokenRequest(value), CancellationToken.None));

        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task RefreshAsync_RejectsADeactivatedUser()
    {
        var user = _users.AddUser("Str0ng!Password1", isActive: false);
        var (value, hash) = RefreshTokenGenerator.Create();
        AddToken(user, hash);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().RefreshAsync(new RefreshTokenRequest(value), CancellationToken.None));

        // Nothing was minted, so a suspended employee cannot extend their session.
        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task RefreshAsync_RejectsAnEmptyToken()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => Service().RefreshAsync(new RefreshTokenRequest("  "), CancellationToken.None));

        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task RefreshAsync_StoresOnlyTheHashOfTheNewToken()
    {
        var user = _users.AddUser("Str0ng!Password1");
        var (value, hash) = RefreshTokenGenerator.Create();
        AddToken(user, hash);

        var response = await Service().RefreshAsync(new RefreshTokenRequest(value), CancellationToken.None);

        Assert.DoesNotContain(_refreshTokens.Tokens, token => token.TokenHash == response.RefreshToken);
        Assert.Contains(_refreshTokens.Tokens, token => token.TokenHash == RefreshTokenGenerator.Hash(response.RefreshToken));
    }

    [Fact]
    public async Task LoginAsync_FailsIdenticallyForAnUnknownEmailAndAWrongPassword()
    {
        _users.AddUser("Str0ng!Password1");

        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().LoginAsync(
                new LoginRequest("nobody@test.local", "WrongPassword1!"),
                CancellationToken.None));

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().LoginAsync(
                new LoginRequest(_users.Users[0].Email!, "WrongPassword1!"),
                CancellationToken.None));

        // Same type and same message: the response cannot be used to enumerate accounts.
        Assert.Equal(unknownEmail.GetType(), wrongPassword.GetType());
        Assert.Equal(unknownEmail.Message, wrongPassword.Message);
        Assert.Empty(_refreshTokens.Tokens);
    }

    private RefreshToken AddToken(
        ApplicationUser user,
        string tokenHash,
        Guid? familyId = null,
        DateTimeOffset? expiresAt = null,
        string? revokedReason = null)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId ?? Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = Now,
            ExpiresAt = expiresAt ?? Now.AddDays(7),
            RevokedAt = revokedReason is null ? null : Now,
            RevokedReason = revokedReason
        };

        _refreshTokens.Tokens.Add(token);
        return token;
    }

    private AuthService Service() => new(
        userManager: _users.UserManager,
        tokenService: _tokenService.Object,
        refreshTokens: _refreshTokens,
        unitOfWork: _unitOfWork,
        loginValidator: new LoginRequestValidator(),
        registerValidator: new RegisterUserRequestValidator(Options.Create(new IdentityOptions())),
        refreshValidator: new RefreshTokenRequestValidator(),
        logoutValidator: new LogoutRequestValidator(),
        tokenOptions: Options.Create(new JwtTokenOptions { RefreshTokenDays = 7 }),
        timeProvider: new FixedTimeProvider(Now),
        logger: NullLogger<AuthService>.Instance);
}