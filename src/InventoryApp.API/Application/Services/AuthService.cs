using FluentValidation;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Common.Options;
using InventoryApp.Application.Dtos;
using InventoryApp.Application.Security;
using InventoryApp.Application.Validators;
using InventoryApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace InventoryApp.Application.Services;

/// <summary>
/// Authentication use-cases. Owns the token lifecycle (issue, rotate, revoke) and delegates
/// password hashing, lockout counters and role storage to Identity's <see cref="UserManager{TUser}"/>.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IValidator<LoginRequest> loginValidator,
    IValidator<RegisterUserRequest> registerValidator,
    IValidator<RefreshTokenRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator,
    IOptions<JwtTokenOptions> tokenOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<UserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.EnsureValidAsync(request, cancellationToken);

        var email = Normalize(request.Email);
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
            IsActive = true
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new ValidationFailedException(ToErrorDictionary(created));

        var roleAssigned = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleAssigned.Succeeded)
        {
            // Roll the account back rather than leaving a user with no role and a working password.
            await userManager.DeleteAsync(user);
            throw new ConflictException(string.Join(
                " ",
                roleAssigned.Errors.Select(error => error.Description)));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} registered with role {Role}.", user.Id, request.Role);

        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.EnsureValidAsync(request, cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email);

        // An unknown email and a wrong password raise the same exception with the same message, so
        // the endpoint cannot be used to discover which addresses have accounts. They also do the
        // same work: nothing is hashed on this path, which is a small residual timing difference.
        if (user is null)
        {
            logger.LogInformation("Login attempted for an unregistered email address.");
            throw UnauthorizedException.InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
            throw UnauthorizedException.AccountLockedOut();

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Increments the counter and applies the lockout once the threshold is reached.
            await userManager.AccessFailedAsync(user);
            throw UnauthorizedException.InvalidCredentials();
        }

        if (!user.IsActive)
            throw new ForbiddenException("This account has been deactivated.");

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var response = await IssueTokenPairAsync(user, roles, familyId: null, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await refreshValidator.EnsureValidAsync(request, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var presentedHash = RefreshTokenGenerator.Hash(request.RefreshToken);
        var stored = await refreshTokens.GetByTokenHashAsync(presentedHash, cancellationToken);

        if (stored is null)
            throw UnauthorizedException.InvalidRefreshToken();

        // This token was already rotated once, so a second presentation means it leaked. Revoke
        // the whole family: assume both the attacker and the legitimate client now hold tokens.
        if (stored.IsReplayed)
        {
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking token family {FamilyId}.",
                stored.UserId,
                stored.FamilyId);

            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, "reused-after-rotation", cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            throw UnauthorizedException.InvalidRefreshToken();
        }

        if (!stored.IsValid(now))
            throw UnauthorizedException.InvalidRefreshToken();

        var user = await userManager.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive)
            throw UnauthorizedException.InvalidRefreshToken();

        var roles = await userManager.GetRolesAsync(user);
        var response = await IssueTokenPairAsync(user, roles, stored.FamilyId, cancellationToken);

        // Retire the presented token in the same transaction that stores its successor, so a crash
        // between the two can never leave a client holding two live refresh tokens.
        stored.RevokedAt = now;
        stored.RevokedReason = "rotated";
        stored.ReplacedByTokenHash = RefreshTokenGenerator.Hash(response.RefreshToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        await logoutValidator.EnsureValidAsync(request, cancellationToken);

        var presentedHash = RefreshTokenGenerator.Hash(request.RefreshToken);
        var stored = await refreshTokens.GetByTokenHashAsync(presentedHash, cancellationToken);

        // Idempotent on purpose: logging out twice, or with a token that never existed, is a success.
        if (stored is null || stored.IsRevoked)
            return;

        stored.RevokedAt = timeProvider.GetUtcNow();
        stored.RevokedReason = "logout";

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mints an access token and tracks a new refresh token. Does not commit: the caller commits
    /// so that rotation can be one atomic write.
    /// </summary>
    private async Task<TokenResponse> IssueTokenPairAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        Guid? familyId,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(user, roles.ToArray());
        var (value, hash) = RefreshTokenGenerator.Create();
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(tokenOptions.Value.RefreshTokenDays);

        await refreshTokens.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId ?? Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = hash,
                CreatedAt = now,
                ExpiresAt = expiresAt
            },
            cancellationToken);

        return new TokenResponse(accessToken.Token, value, accessToken.ExpiresAt, expiresAt);
    }

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, roles.ToArray(), user.CreatedAt);
    }

    private static string Normalize(string email) => email.Trim().ToUpperInvariant();

    /// <summary>
    /// Collapses Identity's flat error list into the field-keyed shape ASP.NET Core already uses
    /// for model binding failures, so clients parse one error format whichever layer rejected them.
    /// </summary>
    private static IDictionary<string, string[]> ToErrorDictionary(IdentityResult result) =>
        result.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "general" : error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).Distinct().ToArray());
}
