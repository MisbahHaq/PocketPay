using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Application.Common.Exceptions;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Auth.Login;

public sealed class LoginHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    // Unknown emails, wrong passwords and lockouts all return the same message so
    // that the endpoint cannot be used to enumerate registered accounts.
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<AuthResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await users.FindByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);

        if (user is null || user.IsLockedOut(now))
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        user.RegisterSuccessfulLogin();

        var accessToken = tokenService.CreateAccessToken(user, now);
        var refreshToken = tokenService.CreateRefreshToken(now);
        refreshTokens.Add(RefreshToken.Create(
            user.Id,
            refreshToken.TokenHash,
            Guid.NewGuid(),
            now,
            refreshToken.ExpiresAtUtc));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.RawValue,
            refreshToken.ExpiresAtUtc);
    }
}
