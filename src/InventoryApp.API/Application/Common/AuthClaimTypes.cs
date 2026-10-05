namespace InventoryApp.Application.Common;

/// <summary>
/// Claim types written into and read from our access tokens.
/// </summary>
/// <remarks>
/// Standard short names rather than the long ClaimTypes URIs. ClaimTypes would work, but it relies
/// on JwtBearer's inbound/outbound claim mapping, which is a well-known source of interop bugs:
/// tokens become unreadable by non-.NET consumers and the mapping behaviour changes between
/// framework versions. Explicit names make the token portable and the contract visible in one file.
/// </remarks>
public static class AuthClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string Name = "unique_name";

    /// <summary>Unique token id, so a specific issued token can be traced or individually revoked later.</summary>
    public const string TokenId = "jti";
}