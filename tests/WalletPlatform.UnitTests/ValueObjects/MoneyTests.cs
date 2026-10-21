using FluentAssertions;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Equality_is_based_on_amount_and_currency()
    {
        var first = new Money(150_00, Currency.PKR);
        var second = new Money(150_00, Currency.PKR);

        first.Should().Be(second);
    }

    [Fact]
    public void Amounts_in_different_currencies_are_not_equal()
    {
        var pkr = new Money(150_00, Currency.PKR);
        var usd = new Money(150_00, Currency.USD);

        pkr.Should().NotBe(usd);
    }

    [Fact]
    public void Addition_sums_minor_units_for_the_same_currency()
    {
        var result = new Money(100_50, Currency.PKR) + new Money(25_25, Currency.PKR);

        result.Should().Be(new Money(125_75, Currency.PKR));
    }

    [Fact]
    public void Subtraction_differences_minor_units_for_the_same_currency()
    {
        var result = new Money(100_00, Currency.PKR) - new Money(150_00, Currency.PKR);

        result.Should().Be(new Money(-50_00, Currency.PKR));
    }

    [Fact]
    public void Addition_throws_when_currencies_differ()
    {
        var add = () => new Money(100, Currency.PKR) + new Money(100, Currency.USD);

        add.Should().Throw<InvalidOperationException>().WithMessage("*PKR*USD*");
    }

    [Fact]
    public void Subtraction_throws_when_currencies_differ()
    {
        var subtract = () => new Money(100, Currency.PKR) - new Money(100, Currency.USD);

        subtract.Should().Throw<InvalidOperationException>().WithMessage("*PKR*USD*");
    }

    [Fact]
    public void Zero_has_no_minor_units_in_the_requested_currency()
    {
        Money.Zero(Currency.PKR).Should().Be(new Money(0, Currency.PKR));
    }
}
