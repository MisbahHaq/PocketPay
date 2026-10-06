using Inventory.Domain.Identity;

namespace Inventory.Application.Auth;

/// <summary>
/// Maps an <see cref="ApplicationUser"/> to the shape the UI consumes.
/// </summary>
/// <remarks>
/// Kept out of the command handlers on purpose. Both login and refresh produce the same
/// user projection, and a shared mapper means the login and refresh responses cannot
/// drift apart - which would show up as a session whose displayed name silently differs
/// depending on how it was obtained.
/// </remarks>
public static class CurrentUserMapper
{
    public static CurrentUserResponse ToResponse(ApplicationUser user, IEnumerable<string> roles) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,

            // Sorted so the client's role list is stable across calls. An unsorted
            // list would make role-gated UI flicker between renders.
            [.. roles.Order(StringComparer.OrdinalIgnoreCase)]);
}