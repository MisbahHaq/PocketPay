using InventoryApp.Domain.Entities;

namespace InventoryApp.Application.Common.Interfaces;

/// <summary>
/// An access token together with the moment it stops being valid. Returning the
/// expiry from the same place that mints the token means the value echoed to the
/// client cannot drift from the token's own <c>exp</c>.
/// </summary>
public sealed record IssuedAccessToken(
    string Token,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Issues signed access tokens. Implemented in Infrastructure because it needs
/// the JWT signing configuration; declared here so the application layer
/// depends on the abstraction, not the crypto library.
/// </summary>
public interface ITokenService
{
    IssuedAccessToken CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles);
}

/// <summary>
/// Generates and hashes refresh tokens.
///
/// The hash is a keyed (HMAC) one-way function: we look tokens up by hash, so
/// there is no need - and no way - to reverse it.
/// </summary>
public interface IRefreshTokenProtector
{
    /// <summary>Creates a new high-entropy random token value to hand to the client.</summary>
    string Generate();

    /// <summary>Computes the value stored in the database for a raw token.</summary>
    string Hash(string rawToken);
}

/// <summary>
/// Persistence for refresh tokens. Kept behind an interface so the application
/// layer stays free of EF Core and so rotation logic can be unit tested against
/// an in-memory store.
/// </summary>
public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks a live token as rotated. Returns false if the token was
    /// already revoked or rotated, so only one of two concurrent callers wins.
    /// Implementations must express this as a single conditional UPDATE.
    /// </summary>
    Task<bool> TryMarkRotatedAsync(
        Guid tokenId,
        Guid replacementTokenId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Revokes every token in a family. Idempotent.</summary>
    Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
