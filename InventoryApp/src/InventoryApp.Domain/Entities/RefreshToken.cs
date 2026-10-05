using InventoryApp.Domain.Constants;

namespace InventoryApp.Domain.Entities;

/// <summary>
/// A single issued refresh token, stored hashed.
///
/// Invariants (private setters + a factory, so the only way to build one is
/// through <see cref="Create"/>):
///   * the raw token is never persisted - only <see cref="TokenHash"/>
///   * a token can be revoked exactly once, and the reason is recorded
///   * rotation links the old token to its replacement via <see cref="ReplacedByTokenId"/>
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
        // Required by EF Core's materialiser.
    }

    private RefreshToken(
        Guid id,
        string userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Every token issued from a single login shares a family id. If a token that
    /// was already rotated is ever presented again we treat it as a stolen-token
    /// replay and revoke the whole family.
    /// </summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>Set when this token is exchanged for a successor.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public ApplicationUser? User { get; set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && !IsExpired(now);

    /// <summary>
    /// A token that has already been rotated but is being presented again - the
    /// signature of a replayed/stolen token rather than an expired session.
    /// </summary>
    public bool WasRotated(DateTimeOffset now) =>
        RevokedAt is not null
        && RevokedReason == RefreshTokenRevocationReasons.Rotated
        && !IsExpired(now);

    public static RefreshToken Create(
        Guid familyId,
        string userId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "Refresh token lifetime must be positive.");
        }

        return new RefreshToken(
            Guid.NewGuid(),
            userId,
            familyId,
            tokenHash,
            now,
            now.Add(lifetime));
    }

    public void MarkRotated(DateTimeOffset now, Guid replacementTokenId)
    {
        EnsureNotRevoked();

        RevokedAt = now;
        RevokedReason = RefreshTokenRevocationReasons.Rotated;
        ReplacedByTokenId = replacementTokenId;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return; // Revocation is idempotent so concurrent logout calls cannot throw.
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

    private void EnsureNotRevoked()
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException(
                $"Refresh token {Id} has already been revoked ({RevokedReason}).");
        }
    }
}
