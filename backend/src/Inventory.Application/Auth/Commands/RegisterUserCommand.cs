using Inventory.Application.Abstractions;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Application.Auth.Commands;

/// <summary>Creates a user account and assigns its starting role.</summary>
public sealed record RegisterUserCommand(RegisterUserRequest Request) : IRequest<CurrentUserResponse>;

/// <summary>
/// Creates an account.
/// </summary>
/// <remarks>
/// The endpoint is restricted to <c>ManageUsers</c> (Admin). Self-registration would be
/// simpler but is wrong for internal warehouse software: an open sign-up means anyone
/// who finds the URL can become Staff, and Staff can move real stock. Accounts are
/// provisioned deliberately by an administrator.
/// </remarks>
public sealed class RegisterUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider dateTime)
    : IRequestHandler<RegisterUserCommand, CurrentUserResponse>
{
    public async Task<CurrentUserResponse> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        var role = string.IsNullOrWhiteSpace(request.Role) ? SystemRoles.Staff : request.Role.Trim();

        if (!SystemRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            throw new ApplicationValidationException(
                $"Unknown role '{role}'.",
                [new Error("role.unknown", $"Role must be one of: {string.Join(", ", SystemRoles.All)}.")
                {
                    Field = nameof(RegisterUserRequest.Role),
                }]);
        }

        var user = new ApplicationUser
        {
            Id = SequentialGuid.New(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            IsActive = true,
            CreatedAt = dateTime.UtcNow,
            // Fresh accounts must go through the lockout policy, otherwise the first
            // five guesses are free for any newly created user.
            LockoutEnabled = true,
        };

        var created = await userManager.CreateAsync(user, request.Password);

        if (!created.Succeeded)
        {
            // Identity enforces password complexity and uniqueness here. Surface its
            // errors verbatim so the form can point at the right field.
            throw new ApplicationValidationException(
                "The account could not be created.",
                created.Errors.Select(error => new Error(
                    string.IsNullOrWhiteSpace(error.Code) ? "invalid" : error.Code,
                    error.Description)
                {
                    Field = MapField(error.Code),
                }));
        }

        var roleAssigned = await userManager.AddToRoleAsync(user, role);

        if (!roleAssigned.Succeeded)
        {
            throw new ConflictException(
                $"Account was created but the role '{role}' could not be assigned.",
                "role-assignment-failed");
        }

        return CurrentUserMapper.ToResponse(user, [role]);
    }

    /// <summary>
    /// Maps an Identity error code to the request field the UI should highlight.
    /// </summary>
    private static string? MapField(string code) => code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => nameof(RegisterUserRequest.Email),
        "PasswordTooShort" or "PasswordRequiresDigit" or "PasswordRequiresLower"
            or "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric"
            or "PasswordTooLong" or "PasswordRequiresUniqueChars" => nameof(RegisterUserRequest.Password),
        _ => null,
    };
}