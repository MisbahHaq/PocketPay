using Inventory.Application.Abstractions;
using Inventory.Application.Common;
using Inventory.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Application.Auth.Queries;

/// <summary>Returns the authenticated user, for hydrating client-side session state.</summary>
public sealed record GetCurrentUserQuery : IRequest<CurrentUserResponse>;

/// <summary>
/// Resolves the caller's profile from their token.
/// </summary>
/// <remarks>
/// Roles come from the database rather than from claims. A token is immutable, so a
/// role claim would keep granting the old permission until the 15-minute token
/// expired; reading the live roles means a demotion takes effect on the next request.
/// The claims are still used to identify the user and to make authorization cheap.
/// </remarks>
public sealed class GetCurrentUserQueryHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<CurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("No authenticated user on this request.");

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new UnauthorizedException("The account for this token no longer exists.");
        }

        if (!user.IsActive)
        {
            // The account was deactivated while a valid token was still in flight.
            throw new UnauthorizedException("This account has been deactivated.", "account-deactivated");
        }

        var roles = await userManager.GetRolesAsync(user);

        return CurrentUserMapper.ToResponse(user, roles);
    }
}