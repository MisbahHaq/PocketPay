using InventoryApp.Application.Auth.Contracts;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Common.Interfaces;
using InventoryApp.Domain.Constants;
using InventoryApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryApp.Application.Auth;

/// <inheritdoc />
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenProtector refreshTokenProtector,
    IRefreshTokenStore refreshTokenStore,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// A stand-in user used only to keep the cost of a failed sign-in identical
    /// whether or not the email exists. It has no password and no identity.
    /// </summary>
    private static readonly ApplicationUser TimingDecoyUser = new() { Id = "timing-decoy" };

    /// <summary>
    /// Hash of a random secret, computed once for the process. The value is
    /// meaningless - only the fact that verifying against it costs the same as
    /// verifying a real password hash matters.
    /// </summary>
    private static readonly Lazy<string> TimingDecoyPasswordHash = new(
        () => new PasswordHasher<ApplicationUser>().HashPassword(
            TimingDecoyUser,
            Guid.NewGuid().ToString("N")),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;

    public async Task<UserCreatedResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        // Re-checked here even though a FluentValidation rule covers it: the
        // service is a public entry point and must not trust its caller.
        if (!ApplicationRoles.All.Contains(request.Role, StringComparer.Ordinal))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] =
                    [$"Role must be one of: {string.Join(", ", ApplicationRoles.All)}."],
            });
        }

        var normalisedEmail = NormaliseEmail(request.Email);
        cancellationToken.ThrowIfCancellationRequested();

        if (await userManager.FindByEmailAsync(normalisedEmail) is not null)
        {
            throw new ConflictException("A user with that email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = normalisedEmail,
            Email = normalisedEmail,
            FullName = request.FullName?.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
            IsActive = true,
        };

        // Identity enforces its own password policy and uniqueness rules here.
        var createResult = await userManager.CreateAsync(user, request.Password);
        EnsureSucceeded(createResult);

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            // Avoid leaving a role-less account behind if role assignment fails.
            await userManager.DeleteAsync(user);

            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] = [.. roleResult.Errors.Select(error => error.Description)],
            });
        }

        logger.LogInformation(
            "User {UserId} ({Email}) created with role {Role}.",
            user.Id,
            user.Email,
            request.Role);

        return new UserCreatedResponse(user.Id, user.Email!, user.FullName, request.Role, user.CreatedAt);
    }

    public async Task<TokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalisedEmail = NormaliseEmail(request.Email);
        var now = timeProvider.GetUtcNow();

        var user = await userManager.FindByEmailAsync(normalisedEmail);
        if (user is null)
        {
            BurnPasswordComparisonTime(request.Password);
            throw new InvalidCredentialsException();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            var lockoutEndsAt = await userManager.GetLockoutEndDateAsync(user);
            BurnPasswordComparisonTime(request.Password);

            logger.LogWarning(
                "Rejected sign-in for {UserId}: account locked out until {LockoutEndsAt}.",
                user.Id,
                lockoutEndsAt);

            throw new AccountLockedException(lockoutEndsAt ?? now);
        }

        if (!user.IsActive)
        {
            // Reported as invalid credentials rather than "disabled", so this
            // endpoint cannot be used to discover deactivated accounts.
            BurnPasswordComparisonTime(request.Password);
            throw new InvalidCredentialsException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Increments the counter and starts the lockout window once the
            // configured threshold is reached.
            await userManager.AccessFailedAsync(user);

            logger.LogWarning(
                "Failed sign-in attempt for {UserId} ({Email}).",
                user.Id,
                user.Email);

            throw new InvalidCredentialsException();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);

        logger.LogInformation("User {UserId} signed in.", user.Id);

        return await IssueNewTokenFamilyAsync(
            user,
            roles,
            familyId: Guid.NewGuid(),
            now,
            cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var presentedToken = await FindByRawTokenAsync(request.RefreshToken, cancellationToken);

        if (presentedToken is null)
        {
            throw new InvalidRefreshTokenException("Refresh token is not recognised.");
        }

        // Replay of an already-rotated token means the token leaked: kill the
        // whole family and force the legitimate user to sign in again.
        if (presentedToken.WasRotated(now))
        {
            await RevokeFamilyAsync(
                presentedToken.FamilyId,
                now,
                RefreshTokenRevocationReasons.ReuseDetected,
                cancellationToken);

            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoked family {FamilyId}.",
                presentedToken.UserId,
                presentedToken.FamilyId);

            throw new InvalidRefreshTokenException("Refresh token has already been used.");
        }

        if (!presentedToken.IsActive(now))
        {
            throw new InvalidRefreshTokenException(
                presentedToken.IsExpired(now)
                    ? "Refresh token has expired."
                    : "Refresh token has been revoked.");
        }

        var user = await userManager.FindByIdAsync(presentedToken.UserId);
        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(
                presentedToken.FamilyId,
                now,
                RefreshTokenRevocationReasons.UserDeactivated,
                cancellationToken);

            throw new InvalidRefreshTokenException("Refresh token is no longer valid.");
        }

        // The successor is created first so the replacement link is always
        // resolvable, then the old token is retired with a compare-and-swap.
        var (replacement, rawReplacement) = CreateRefreshToken(
            user.Id,
            presentedToken.FamilyId,
            now);

        var rotated = await refreshTokenStore.TryMarkRotatedAsync(
            presentedToken.Id,
            replacement.Id,
            now,
            cancellationToken);

        if (!rotated)
        {
            // Lost the race against a concurrent refresh of the same token.
            throw new InvalidRefreshTokenException("Refresh token has already been used.");
        }

        await refreshTokenStore.AddAsync(replacement, cancellationToken);
        await refreshTokenStore.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.CreateAccessToken(user, roles);

        return new TokenResponse(
            accessToken.Token,
            rawReplacement,
            accessToken.ExpiresAt,
            replacement.ExpiresAt);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var presentedToken = await FindByRawTokenAsync(request.RefreshToken, cancellationToken);

        if (presentedToken is null)
        {
            // No exception and no logging: logout must never confirm whether a
            // token was real.
            return;
        }

        // Revoke the family, not just this token. Because rotation retires the
        // previous token every time, the newest token in the family would
        // otherwise survive a logout performed with a stale token.
        var revokedCount = await RevokeFamilyAsync(
            presentedToken.FamilyId,
            now,
            RefreshTokenRevocationReasons.LoggedOut,
            cancellationToken);

        logger.LogInformation(
            "User {UserId} logged out; {RevokedCount} refresh token(s) revoked.",
            presentedToken.UserId,
            revokedCount);
    }

    private async Task<TokenResponse> IssueNewTokenFamilyAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (refreshToken, rawRefreshToken) = CreateRefreshToken(user.Id, familyId, now);

        await refreshTokenStore.AddAsync(refreshToken, cancellationToken);
        await refreshTokenStore.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(
            user,
            roles as IReadOnlyCollection<string> ?? [.. roles]);

        return new TokenResponse(
            accessToken.Token,
            rawRefreshToken,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt);
    }

    private (RefreshToken Token, string RawValue) CreateRefreshToken(
        string userId,
        Guid familyId,
        DateTimeOffset now)
    {
        var rawValue = refreshTokenProtector.Generate();

        var token = RefreshToken.Create(
            familyId,
            userId,
            refreshTokenProtector.Hash(rawValue),
            now,
            _refreshTokenOptions.Lifetime);

        return (token, rawValue);
    }

    private async Task<RefreshToken?> FindByRawTokenAsync(
        string rawToken,
        CancellationToken cancellationToken) =>
        await refreshTokenStore.FindByHashAsync(
            refreshTokenProtector.Hash(rawToken),
            cancellationToken);

    private async Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken)
    {
        var revokedCount = await refreshTokenStore.RevokeFamilyAsync(
            familyId,
            now,
            reason,
            cancellationToken);

        await refreshTokenStore.SaveChangesAsync(cancellationToken);
        return revokedCount;
    }

    /// <summary>
    /// Performs the expensive part of a password check against a throwaway hash
    /// so that a failed sign-in costs the same whether or not the account exists.
    /// </summary>
    private void BurnPasswordComparisonTime(string password) =>
        _ = passwordHasher.VerifyHashedPassword(
            TimingDecoyUser,
            TimingDecoyPasswordHash.Value,
            password);

    private static string NormaliseEmail(string email) => email.Trim().ToUpperInvariant();

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var error in result.Errors)
        {
            var key = MapIdentityCodeToField(string.IsNullOrEmpty(error.Code) ? "general" : error.Code);

            if (!errors.TryGetValue(key, out var messages))
            {
                messages = [];
                errors[key] = messages;
            }

            messages.Add(error.Description);
        }

        throw new ValidationFailedException(
            errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
    }

    /// <summary>
    /// Turns Identity's machine-readable error codes into request field names so
    /// the client can highlight the offending input field.
    /// </summary>
    private static string MapIdentityCodeToField(string code) => code switch
    {
        "PasswordTooShort" => "Password",
        "PasswordRequiresUniqueChars" => "Password",
        "PasswordRequiresNonAlphanumeric" => "Password",
        "PasswordRequiresDigit" => "Password",
        "PasswordRequiresUpper" => "Password",
        "PasswordRequiresLower" => "Password",
        "DuplicateUserName" => "Email",
        "DuplicateEmail" => "Email",
        "InvalidUserName" => "Email",
        "InvalidEmail" => "Email",
        _ => "general",
    };
}
