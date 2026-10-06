using System.Security.Cryptography;
using Inventory.Domain.Common;

namespace Inventory.Domain.Identity;

/// <summary>
/// A single issued refresh token, stored hashed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rotation.</b> Every use of a refresh token issues a new one and revokes the old
/// one. Long-lived, reusable refresh tokens are stolen routinely - they travel in
/// less-protected contexts than access tokens and live for days.
/// </para>
/// <para>
/// <b>Replay detection.</b> Presenting an already-revoked token means either an
/// attacker replayed a stolen copy or a legitimate client raced itself. We cannot
/// always tell which, so we assume compromise and revoke the entire
/// <see cref="FamilyId"/> chain, forcing a fresh login. That is the correct
/// trade-off: it costs one login, whereas trusting the token costs an account.
/// </para>
/// <para>
/// <b>Hashing.</b> Only the SHA-256 hash is persisted. A database dump therefore does
/// not hand out usable sessions. SHA-256 (not bcrypt/argon2) is the right primitive
/// here because the input is 512 bits of CSPRNG output and therefore has no
/// guessable structure to slow down.
/// </para>
/// </remarks>
public class RefreshToken : Entity
{
    /// <summary>Maximum lifetime of a single refresh token, in days.</summary>
    public const int DefaultLifetimeDays = 7;

    private RefreshToken()
    {
    }

    private RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    /// <summary>Identifies the rotation chain this token belongs to.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Base64url-encoded SHA-256 of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>Hash of the token that superseded this one, closing the rotation chain.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// True when this token was revoked by rotation but is being presented anyway -
    /// the signal that triggers family revocation.
    /// </summary>
    public bool IsReplayed => IsRevoked && RevokedReason == RefreshTokenRevocationReasons.Rotated;

    /// <summary>
    /// Generates a new token and its persisted row.
    /// </summary>
    /// <param name="userId">Owner of the token.</param>
    /// <param name="familyId">
    /// Rotation chain identifier. Pass the current family's id to continue a chain,
    /// or <c>null</c> to start a new one at login.
    /// </param>
    /// <param name="now">Current instant, injected so expiry is testable.</param>
    /// <param name="lifetimeDays">Lifetime, capped by the configured policy.</param>
    /// <returns>The row to persist, plus the raw token to return to the client once.</returns>
    public static (RefreshToken Token, string RawToken) Create(
        Guid userId,
        Guid? familyId,
        DateTimeOffset now,
        int lifetimeDays = DefaultLifetimeDays)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A refresh token must belong to a real user.", nameof(userId));
        }

        var rawToken = GenerateRawToken();

        var token = new RefreshToken(
            userId,
            familyId ?? Guid.NewGuid(),
            HashToken(rawToken),
            now,
            now.AddDays(lifetimeDays));

        return (token, rawToken);
    }

    /// <summary>Revokes this token in favour of <paramref name="replacementHash"/>.</summary>
    public void RevokedByRotation(string replacementHash, DateTimeOffset now)
    {
        RevokedAt ??= now;
        RevokedReason ??= RefreshTokenRevocationReasons.Rotated;
        ReplacedByTokenHash = replacementHash;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

    private static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[64]; // 512 bits
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Hashes a raw token for storage and lookup. Deliberately public so the
    /// refresh endpoint can find a token without ever constructing an entity first.
    /// </summary>
    public static string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}