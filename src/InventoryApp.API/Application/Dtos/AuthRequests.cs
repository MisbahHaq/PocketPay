namespace InventoryApp.Application.Dtos;

/// <summary>Admin-only request to provision a user with a starting role.</summary>
public sealed record RegisterUserRequest(string Email, string FullName, string Password, string Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);
