namespace WalletPlatform.Domain.Enums;

/// <summary>
/// The side of a double-entry posting. The direction carries the sign, so amounts
/// stored on a posting are always positive; a debit and a credit of the same account
/// cancel each other out.
/// </summary>
public enum Direction
{
    Debit = 0,
    Credit = 1,
}
