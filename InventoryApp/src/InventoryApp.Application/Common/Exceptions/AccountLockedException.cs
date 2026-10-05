using InventoryApp.Application.Common.Exceptions;

/// <summary>
/// 401 - the account exists but is temporarily locked out after too many failed
/// attempts.
/// </summary>
public sealed class AccountLockedException : ApplicationExceptionBase
{
    public AccountLockedException(DateTimeOffset lockoutEndsAt)
        : base("This account is temporarily locked because of too many failed sign-in attempts.")
    {
        LockoutEndsAt = lockoutEndsAt;
    }

    public DateTimeOffset LockoutEndsAt { get; }
}
