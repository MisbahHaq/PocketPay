using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryApp.Domain.Entities;

/// <summary>
/// One issued refresh token. A new row is created on every rotation, so the table doubles as
/// an audit trail of every token that has ever been valid for an account.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>
    /// Shared by every token descended from one login. Replaying an already-rotated token
    /// means it leaked, so the whole family is revoked.
    /// </summary>
    public Guid FamilyId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    /// <summary>Base64Url-encoded SHA-256 of the token value. The plaintext is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    /// <summary>Hash of the token that superseded this one. Its presence marks this row as rotated.</summary>
    public string? ReplacedByTokenHash { get; set; }

    [NotMapped]
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Valid means neither rotated/revoked nor past its absolute expiry.</summary>
    public bool IsValid(DateTimeOffset now) => !IsRevoked && ExpiresAt > now;

    /// <summary>
    /// True when this token was already rotated but is being presented again, i.e. a stolen
    /// token replayed by an attacker alongside the legitimate client.
    /// </summary>
    public bool IsReplayed => IsRevoked && ReplacedByTokenHash is not null;
}
