namespace InventoryApp.Application.Auth;

/// <summary>
/// Claim type names written into access tokens. Centralised so the token
/// service and the controllers cannot drift apart.
/// </summary>
public static class AuthClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string TokenId = "jti";
    public const string Role = "role";
}
