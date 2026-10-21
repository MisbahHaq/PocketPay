namespace WalletPlatform.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    // Only the SHA-256 hash of the token is persisted; the raw value never touches the DB.
    public string TokenHash { get; private set; } = null!;

    // Tokens issued through the same login session share a family id. Presenting any
    // token from a family after rotation revokes the whole family (reuse detection).
    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (familyId == Guid.Empty)
        {
            throw new ArgumentException("Family id is required.", nameof(familyId));
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Expiry must be after creation.", nameof(expiresAtUtc));
        }

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    public void Revoke(DateTimeOffset nowUtc)
    {
        RevokedAtUtc ??= nowUtc;
    }

    public void MarkReplacedBy(Guid replacementTokenId) => ReplacedByTokenId = replacementTokenId;
}
