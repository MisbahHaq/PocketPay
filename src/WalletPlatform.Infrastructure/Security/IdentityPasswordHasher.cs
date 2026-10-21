using Microsoft.AspNetCore.Identity;
using WalletPlatform.Application.Abstractions.Security;
using WalletPlatform.Domain.Enums;
using DomainUser = WalletPlatform.Domain.Entities.User;

namespace WalletPlatform.Infrastructure.Security;

/// <summary>
/// Thin adapter over ASP.NET Core Identity's PBKDF2 implementation. It is used
/// standalone (without the rest of Identity) so that password storage can change
/// later without touching the Application layer.
/// </summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly DomainUser HasherContext = DomainUser.Create(
        "password-hasher@paypocket.local",
        "not-a-real-password-hash",
        Role.Customer,
        DateTimeOffset.UnixEpoch);

    private readonly PasswordHasher<DomainUser> _passwordHasher = new();

    public string Hash(string password) => _passwordHasher.HashPassword(HasherContext, password);

    public bool Verify(string password, string passwordHash)
    {
        var result = _passwordHasher.VerifyHashedPassword(HasherContext, passwordHash, password);

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
