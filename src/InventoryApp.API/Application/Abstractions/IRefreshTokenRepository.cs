using InventoryApp.Domain.Entities;

namespace InventoryApp.Application.Abstractions;

/// <summary>
/// Persistence boundary for refresh tokens. Kept behind an interface so rotation logic can be
/// unit-tested without a database, and so the Application layer never sees a DbContext.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Tracks a newly issued token. Persisted when the unit of work is committed.</summary>
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every live token in a family in one statement. Used for refresh token reuse
    /// detection, where revoking only the presented token would let the attacker keep going.
    /// </summary>
    Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken);
}
