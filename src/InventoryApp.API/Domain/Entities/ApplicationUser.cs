using Microsoft.AspNetCore.Identity;

namespace InventoryApp.Domain.Entities;

/// <summary>
/// Aggregate root for the identity bounded context. Extends the Identity schema instead of
/// replacing it so password hashing, lockout counters and role storage come for free.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-delete / offboarding flag. Checked on login and on every refresh.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
