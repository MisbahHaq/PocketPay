namespace InventoryApp.Domain.Constants;

/// <summary>
/// Why a refresh token is no longer usable. Kept as data so the audit trail
/// explains *how* a session ended without needing to correlate log files.
/// </summary>
public static class RefreshTokenRevocationReasons
{
    public const string Rotated = "Rotated";
    public const string LoggedOut = "LoggedOut";
    public const string ReuseDetected = "ReuseDetected";
    public const string UserDeactivated = "UserDeactivated";
}
