using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Domain.ValueObjects;

/// <summary>
/// A monetary amount in minor units (e.g. paisa for PKR) so that money is never
/// represented by binary floating point. 100 minor units == 1 major unit.
/// </summary>
public sealed record Money(long MinorUnits, Currency Currency)
{
    public static Money Zero(Currency currency) => new(0, currency);

    public static Money operator +(Money left, Money right) =>
        new(checked(left.MinorUnits + right.MinorUnits), EnsureSameCurrency(left, right));

    public static Money operator -(Money left, Money right) =>
        new(checked(left.MinorUnits - right.MinorUnits), EnsureSameCurrency(left, right));

    private static Currency EnsureSameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? left.Currency
            : throw new InvalidOperationException(
                $"Cannot combine amounts in {left.Currency} and {right.Currency}.");
}
