using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Services;

/// <summary>User administration. Every operation is reached through the CanManageUsers policy.</summary>
public sealed class UserService(UserManager<ApplicationUser> userManager) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var responses = new List<UserResponse>(users.Count);
        foreach (var user in users)
            responses.Add(await ToResponseAsync(user, cancellationToken));

        return responses;
    }

    public async Task<UserResponse> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindOrThrowAsync(userId);
        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<UserResponse> SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken)
    {
        var user = await FindOrThrowAsync(userId);

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
            throw new ConflictException(string.Join(" ", result.Errors.Select(error => error.Description)));

        // Offboarding must also cut live sessions, not just block the next login.
        if (!isActive)
            await userManager.UpdateSecurityStampAsync(user);

        return await ToResponseAsync(user, cancellationToken);
    }

    private async Task<ApplicationUser> FindOrThrowAsync(string userId) =>
        await userManager.FindByIdAsync(userId) ?? throw new NotFoundException($"User '{userId}' was not found.");

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // One roles query per user. Fine at this scale; a "users with roles" projection would be the
        // fix if this list ever needs to page over thousands of accounts.
        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, roles.ToArray(), user.CreatedAt);
    }
}
