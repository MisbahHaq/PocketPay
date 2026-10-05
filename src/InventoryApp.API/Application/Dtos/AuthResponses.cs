namespace InventoryApp.Application.Dtos;

/// <summary>Issued token pair. The refresh token is returned exactly once, at issue time.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType = "Bearer");

/// <summary>User projection safe to return to any caller: never contains the password hash.</summary>
public sealed record UserResponse(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset CreatedAt);

/// <summary>Identity of the caller, read from the validated access token's claims.</summary>
public sealed record CurrentUserResponse(string Id, string Email, IReadOnlyCollection<string> Roles);
