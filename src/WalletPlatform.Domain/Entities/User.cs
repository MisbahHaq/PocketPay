using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Domain.Entities;

public sealed class User
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Role Role { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User Create(string email, string passwordHash, Role role, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Role = role,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool IsLockedOut(DateTimeOffset nowUtc) =>
        LockoutEndUtc is { } lockoutEnd && lockoutEnd > nowUtc;

    public void RegisterFailedLogin(DateTimeOffset nowUtc)
    {
        // A lockout that has already expired starts a fresh window of attempts.
        if (LockoutEndUtc is { } lockoutEnd && lockoutEnd <= nowUtc)
        {
            FailedLoginAttempts = 0;
            LockoutEndUtc = null;
        }

        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockoutEndUtc = nowUtc.Add(LockoutDuration);
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
    }
}
