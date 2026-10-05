namespace InventoryApp.Application.Abstractions;

/// <summary>
/// Reads the caller's identity from the current HTTP request. Implemented over HttpContext so
/// services can log or filter by the acting user without taking a user id parameter everywhere.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }
}
