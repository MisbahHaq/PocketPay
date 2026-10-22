using FluentAssertions;
using FluentValidation;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Auth;
using WalletPlatform.Application.Auth.Login;
using WalletPlatform.Application.Auth.Refresh;
using WalletPlatform.Application.Auth.RegisterUser;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Application.Common.Security;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.UnitTests.Fakes;

namespace WalletPlatform.UnitTests.Auth;

public sealed class RegisterUserHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeWalletRepository _wallets = new();
    private readonly FakeWalletNumberGenerator _walletNumbers = new("123456789012");
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(Now);

    private RegisterUserHandler CreateHandler() =>
        new(_users, _wallets, _walletNumbers, _passwordHasher, _unitOfWork, _clock);

    [Fact]
    public async Task Handle_creates_user_and_wallet_inside_a_transaction()
    {
        var response = await CreateHandler().HandleAsync(
            new RegisterUserRequest("  User@Example.com ", "Str0ng!Pass1"),
            CancellationToken.None);

        response.Email.Should().Be("user@example.com");
        response.WalletNumber.Should().Be("123456789012");

        _users.Items.Should().ContainSingle();
        _wallets.Items.Should().ContainSingle();
        _unitOfWork.TransactionCount.Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(2);
        _users.Items[0].PasswordHash.Should().Be("hashed::Str0ng!Pass1");
        _wallets.Items[0].UserId.Should().Be(_users.Items[0].Id);
    }

    [Fact]
    public async Task Handle_throws_conflict_when_email_already_exists()
    {
        _users.Add(User.Create("user@example.com", "hash", Role.Customer, Now));

        var act = () => CreateHandler().HandleAsync(
            new RegisterUserRequest("USER@example.com", "Str0ng!Pass1"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _walletNumbers.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_generates_wallet_number_exactly_once()
    {
        await CreateHandler().HandleAsync(
            new RegisterUserRequest("user@example.com", "Str0ng!Pass1"),
            CancellationToken.None);

        _walletNumbers.Calls.Should().Be(1);
    }
}

public sealed class LoginHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeTokenService _tokenService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(Now);

    private LoginHandler CreateHandler() =>
        new(_users, _refreshTokens, _passwordHasher, _tokenService, _unitOfWork, _clock);

    private void SeedUser(string password = "Str0ng!Pass1") =>
        _users.Add(User.Create("user@example.com", _passwordHasher.Hash(password), Role.Customer, Now));

    [Fact]
    public async Task Handle_returns_tokens_and_persists_refresh_token_on_success()
    {
        SeedUser();

        var response = await CreateHandler().HandleAsync(
            new LoginRequest("USER@example.com", "Str0ng!Pass1"),
            CancellationToken.None);

        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
        _refreshTokens.Items.Should().ContainSingle();
        _refreshTokens.Items[0].TokenHash.Should().Be(_tokenService.HashRefreshToken(response.RefreshToken));
    }

    [Fact]
    public async Task Handle_throws_generic_unauthorized_for_unknown_email()
    {
        var act = () => CreateHandler().HandleAsync(
            new LoginRequest("nobody@example.com", "Str0ng!Pass1"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_locks_account_after_repeated_failures()
    {
        SeedUser();

        for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
        {
            var act = () => CreateHandler().HandleAsync(
                new LoginRequest("user@example.com", "WrongPass!1"),
                CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        _users.Items[0].IsLockedOut(Now).Should().BeTrue();

        var lockedAct = () => CreateHandler().HandleAsync(
            new LoginRequest("user@example.com", "Str0ng!Pass1"),
            CancellationToken.None);

        await lockedAct.Should().ThrowAsync<UnauthorizedException>();
    }
}

public sealed class RefreshTokenHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeTokenService _tokenService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(Now);

    private RefreshTokenHandler CreateHandler() =>
        new(_users, _refreshTokens, _tokenService, _unitOfWork, _clock);

    [Fact]
    public async Task Handle_rotates_token_within_the_same_family()
    {
        var user = User.Create("user@example.com", "hash", Role.Customer, Now);
        _users.Add(user);

        var issued = _tokenService.CreateRefreshToken(Now);
        var familyId = Guid.NewGuid();
        _refreshTokens.Add(RefreshToken.Create(user.Id, issued.TokenHash, familyId, Now, issued.ExpiresAtUtc));

        var response = await CreateHandler().HandleAsync(
            new RefreshTokenRequest(issued.RawValue),
            CancellationToken.None);

        response.RefreshToken.Should().NotBe(issued.RawValue);
        _refreshTokens.Items.Should().HaveCount(2);

        var original = _refreshTokens.Items.Single(token => token.TokenHash == issued.TokenHash);
        original.IsRevoked.Should().BeTrue();
        original.ReplacedByTokenId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_revokes_entire_family_when_a_revoked_token_is_reused()
    {
        var user = User.Create("user@example.com", "hash", Role.Customer, Now);
        _users.Add(user);

        var familyId = Guid.NewGuid();
        var first = _tokenService.CreateRefreshToken(Now);
        var firstToken = RefreshToken.Create(user.Id, first.TokenHash, familyId, Now, first.ExpiresAtUtc);
        _refreshTokens.Add(firstToken);

        var rotated = await CreateHandler().HandleAsync(
            new RefreshTokenRequest(first.RawValue),
            CancellationToken.None);

        var secondHash = _tokenService.HashRefreshToken(rotated.RefreshToken);

        var reuseAct = () => CreateHandler().HandleAsync(
            new RefreshTokenRequest(first.RawValue),
            CancellationToken.None);

        await reuseAct.Should().ThrowAsync<UnauthorizedException>();

        _refreshTokens.Items.Where(token => token.FamilyId == familyId)
            .Should().OnlyContain(token => token.IsRevoked);

        var familyTokens = _refreshTokens.Items.Where(token => token.FamilyId == familyId).ToList();
        familyTokens.Should().Contain(token => token.TokenHash == secondHash);
    }
}

public sealed class ContractRedactionTests
{
    [Fact]
    public void Request_contracts_do_not_reveal_secrets_in_ToString()
    {
        var password = "SuperSecret!123";
        var refresh = "raw-refresh-token-value";

        new RegisterUserRequest("user@example.com", password).ToString()
            .Should().NotContain(password);

        new LoginRequest("user@example.com", password).ToString()
            .Should().NotContain(password);

        new RefreshTokenRequest(refresh).ToString()
            .Should().NotContain(refresh);

        new AuthResponse("access-token-value", DateTimeOffset.UnixEpoch, refresh, DateTimeOffset.UnixEpoch)
            .ToString()
            .Should().NotContain("access-token-value")
            .And.NotContain(refresh);
    }
}
