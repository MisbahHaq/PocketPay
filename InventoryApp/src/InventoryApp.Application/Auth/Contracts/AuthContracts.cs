namespace InventoryApp.Application.Auth.Contracts;

/// <summary>Register a new user. Admin-only endpoint.</summary>
public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string? FullName,
    string Role);

/// <summary>Exchange credentials for a token pair.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Exchange a refresh token for a new pair (rotating the refresh token).</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>Revoke a refresh token, ending the session.</summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// The token pair returned to a client. <paramref name="RefreshToken"/> is only
/// ever present in this response - the server stores its hash, never the value.
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType = "Bearer");

/// <summary>Shape returned by the register endpoint.</summary>
public sealed record UserCreatedResponse(
    string Id,
    string Email,
    string? FullName,
    string Role,
    DateTimeOffset CreatedAt);
