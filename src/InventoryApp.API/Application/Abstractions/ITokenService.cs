using InventoryApp.Domain.Entities;

namespace InventoryApp.Application.Abstractions;

/// <summary>Creates signed access tokens. Implemented in Infrastructure because JWT is a technical concern.</summary>
public interface ITokenService
{
    AccessTokenResult CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
