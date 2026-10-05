using Microsoft.AspNetCore.Identity;

namespace InventoryApp.Domain.Entities;

/// <summary>
/// Identity's user entity plus the profile fields this system needs.
/// Kept in Domain (rather than Infrastructure) so the application layer can
/// reference it without referencing EF Core or the web tier.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft switch for deactivating an account without deleting history.
    /// Enforced by the application layer, not by a database constraint, because
    /// "must be active" is a business rule that also applies to refresh tokens.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
