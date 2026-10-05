using InventoryApp.Application.Dtos;

namespace InventoryApp.Application.Abstractions;

/// <summary>User administration use-cases. Reserved for Admin via the CanManageUsers policy.</summary>
public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken);

    Task<UserResponse> GetByIdAsync(string userId, CancellationToken cancellationToken);

    Task<UserResponse> SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken);
}
