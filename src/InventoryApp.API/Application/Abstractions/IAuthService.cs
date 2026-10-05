using InventoryApp.Application.Dtos;

namespace InventoryApp.Application.Abstractions;

/// <summary>Use-case boundary for authentication. Controllers depend on this, not on Identity.</summary>
public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken);

    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Exchanges a refresh token for a new pair and rotates the presented token.</summary>
    Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    /// <summary>Revokes the presented refresh token. Idempotent, and succeeds for unknown tokens.</summary>
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}
