using Inventory.Domain.Identity;

namespace Inventory.Application.Abstractions;

/// <summary>
/// Persistence operations for refresh tokens, kept out of the handlers so they never
/// need EF Core's bulk-update extensions.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Finds a token by its stored hash. Returns null when absent or soft-deleted.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every active token in a rotation chain in a single statement.
    /// </summary>
    /// <remarks>
    /// Must be a set-based update, not a loop over tracked entities. This runs on the
    /// compromise-detection path, where the chain may be long and where we want one
    /// round trip rather than one per token.
    /// </remarks>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active token belonging to a user (used on deactivation and password change).</summary>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken = default);
}