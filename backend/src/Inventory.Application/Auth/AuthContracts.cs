namespace Inventory.Application.Auth;

/// <summary>Credentials submitted to the token endpoint.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Body of the refresh endpoint. Carries the refresh token returned at login.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>Body of the logout endpoint. Revokes the presented refresh token.</summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// Account creation. Role is optional and, when omitted, produces a Staff account -
/// the lowest privilege - so that a compromised admin cannot accidentally mass-create
/// administrators.
/// </summary>
public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FullName,
    string? Role = null);

/// <summary>The signed-in principal, as the UI needs to render menus and gate screens.</summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

/// <summary>
/// A successful authentication result.
/// </summary>
/// <param name="AccessToken">Short-lived bearer token.</param>
/// <param name="ExpiresAt">Absolute expiry, so the client can refresh proactively instead of on failure.</param>
/// <param name="RefreshToken">
/// Opaque, single-use, rotation-protected token. Handed to the browser once and never
/// again - it is only ever read back on the refresh endpoint.
/// </param>
public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserResponse User);