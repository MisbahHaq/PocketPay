using InventoryApp.Application.Abstractions;
using InventoryApp.Domain.Entities;

namespace InventoryApp.Tests.Support;

/// <summary>
/// In-memory stand-in for the refresh token repository. Hand-written rather than mocked because the
/// rotation tests need real state transitions (finding by hash, revoking a family) to assert on.
/// </summary>
internal sealed class TestRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken)
    {
        var affected = Tokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt is null)
            .ToList();

        foreach (var token in affected)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }

        return Task.FromResult(affected.Count);
    }
}