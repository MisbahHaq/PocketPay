namespace Inventory.Application.Abstractions;

/// <summary>
/// Who is making the current request.
/// </summary>
/// <remarks>
/// Deliberately a narrow interface rather than passing <c>ClaimsPrincipal</c> around:
/// handlers stay unit-testable, and the "no authenticated user" case (seeding,
/// background jobs) is representable by <see cref="IsAuthenticated"/> being false
/// instead of by a null principal.
/// </remarks>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    /// <summary>User id, or null when running outside a request (seeding, background work).</summary>
    Guid? UserId { get; }

    string? Email { get; }

    string? FullName { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}