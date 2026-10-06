namespace Inventory.Domain.Identity;

/// <summary>
/// Why a refresh token was revoked. Persisted verbatim so the audit trail can
/// distinguish an ordinary logout from a compromise response.
/// </summary>
public static class RefreshTokenRevocationReasons
{
    /// <summary>Superseded by a new token during a normal refresh.</summary>
    public const string Rotated = "rotated";

    /// <summary>The user signed out.</summary>
    public const string Logout = "logout";

    /// <summary>Revoked administratively.</summary>
    public const string Administrative = "administrative";

    /// <summary>A revoked token was presented, so the whole family was revoked.</summary>
    public const string ReuseDetected = "reuse-detected";

    /// <summary>The owning account was deactivated.</summary>
    public const string UserDeactivated = "user-deactivated";

    public static readonly IReadOnlyList<string> All =
    [
        Rotated,
        Logout,
        Administrative,
        ReuseDetected,
        UserDeactivated,
    ];
}