using FluentAssertions;
using WalletPlatform.Application.Common;
using WalletPlatform.Application.Transfers.Policies;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.ValueObjects;
using WalletPlatform.UnitTests.Fakes;

namespace WalletPlatform.UnitTests.Transfers.Policies;

public sealed class DestinationWalletPolicyTests
{
    [Fact]
    public void Fails_when_destination_is_null()
    {
        var policy = new DestinationWalletPolicy();

        var result = policy.Check(Guid.NewGuid(), null, "123456789012");

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.DestinationNotFound);
    }

    [Fact]
    public void Fails_when_source_equals_destination()
    {
        var walletId = Guid.NewGuid();
        var policy = new DestinationWalletPolicy();

        var result = policy.Check(walletId, walletId, "123456789012");

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.SameWallet);
    }

    [Fact]
    public void Succeeds_when_different_wallets()
    {
        var policy = new DestinationWalletPolicy();

        var result = policy.Check(Guid.NewGuid(), Guid.NewGuid(), "123456789012");

        result.IsSuccess.Should().BeTrue();
    }
}

public sealed class CurrencyMatchPolicyTests
{
    [Fact]
    public void Fails_when_currencies_differ()
    {
        var policy = new CurrencyMatchPolicy();

        var result = policy.Check(Currency.PKR, Currency.USD);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.CurrencyMismatch);
    }

    [Theory]
    [InlineData(Currency.PKR, Currency.PKR)]
    [InlineData(Currency.USD, Currency.USD)]
    public void Succeeds_when_currencies_match(Currency a, Currency b)
    {
        var policy = new CurrencyMatchPolicy();

        var result = policy.Check(a, b);

        result.IsSuccess.Should().BeTrue();
    }
}

public sealed class AmountLimitsPolicyTests
{
    private static readonly TransferOptions Options = new()
    {
        MinAmountMinorUnits = 100,
        MaxAmountMinorUnits = 1_000_000,
        FlatFeeMinorUnits = 50,
    };

    [Fact]
    public void Fails_when_amount_below_minimum()
    {
        var policy = new AmountLimitsPolicy(Options);
        var amount = new Money(50, Currency.PKR);
        var fee = new Money(50, Currency.PKR);

        var result = policy.Check(amount, fee, availableBalanceMinorUnits: 10_000);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.AmountBelowMinimum);
    }

    [Fact]
    public void Fails_when_amount_above_maximum()
    {
        var policy = new AmountLimitsPolicy(Options);
        var amount = new Money(2_000_000, Currency.PKR);
        var fee = new Money(50, Currency.PKR);

        var result = policy.Check(amount, fee, availableBalanceMinorUnits: 10_000_000);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.AmountAboveMaximum);
    }

    [Fact]
    public void Fails_when_insufficient_funds()
    {
        var policy = new AmountLimitsPolicy(Options);
        var amount = new Money(10_000, Currency.PKR);
        var fee = new Money(50, Currency.PKR);

        var result = policy.Check(amount, fee, availableBalanceMinorUnits: 5_000);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.InsufficientFunds);
    }

    [Fact]
    public void Succeeds_when_amount_in_range_and_funds_sufficient()
    {
        var policy = new AmountLimitsPolicy(Options);
        var amount = new Money(10_000, Currency.PKR);
        var fee = new Money(50, Currency.PKR);

        var result = policy.Check(amount, fee, availableBalanceMinorUnits: 20_000);

        result.IsSuccess.Should().BeTrue();
    }
}

public sealed class DailyLimitPolicyTests
{
    private static readonly TransferOptions Options = new()
    {
        DailyOutgoingLimitMinorUnits = 50_000,
    };

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Fails_when_projected_exceeds_limit()
    {
        var policy = new DailyLimitPolicy(Options, _clock);
        var amount = new Money(40_000, Currency.PKR);
        var fee = new Money(0, Currency.PKR);

        // Already sent 20,000 in the last 24h; this transfer would make it 60,000 > 50,000
        var result = policy.Check(completedOutgoingSumMinorUnits: 20_000, amount, fee);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(TransferErrorCodes.DailyLimitExceeded);
    }

    [Fact]
    public void Succeeds_when_within_limit()
    {
        var policy = new DailyLimitPolicy(Options, _clock);
        var amount = new Money(20_000, Currency.PKR);
        var fee = new Money(0, Currency.PKR);

        var result = policy.Check(completedOutgoingSumMinorUnits: 20_000, amount, fee);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Includes_fee_in_calculation()
    {
        var policy = new DailyLimitPolicy(Options, _clock);
        var amount = new Money(49_000, Currency.PKR);
        var fee = new Money(1_000, Currency.PKR); // total = 50,000 exactly

        var result = policy.Check(completedOutgoingSumMinorUnits: 0, amount, fee);

        result.IsSuccess.Should().BeTrue();
    }
}