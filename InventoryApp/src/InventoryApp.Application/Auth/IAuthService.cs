using InventoryApp.Application.Auth.Contracts;

namespace InventoryApp.Application.Auth;

/// <summary>Authentication use cases. Implemented by <c>AuthService</c>.</summary>
public interface IAuthService
{
    Task<UserCreatedResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken);

    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for a fresh pair, rotating the refresh token.
    /// </summary>
    Task<TokenResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the session that the supplied refresh token belongs to. Always
    /// succeeds so callers cannot use it to probe for valid tokens.
    /// </summary>
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}
