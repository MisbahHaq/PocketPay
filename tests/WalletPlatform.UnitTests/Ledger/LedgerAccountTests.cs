using FluentAssertions;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Exceptions;

namespace WalletPlatform.UnitTests.Ledger;

public sealed class LedgerAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Wallet_account_is_a_credit_normal_liability()
    {
        var account = LedgerAccount.CreateForWallet(Guid.NewGuid(), Currency.PKR, Now);

        account.Type.Should().Be(AccountType.Liability);
        account.IsCreditNormal.Should().BeTrue();
        account.SystemCode.Should().BeNull();
        account.WalletId.Should().NotBeNull();
    }

    [Theory]
    [InlineData(SystemAccount.Funding, AccountType.Asset, false)]
    [InlineData(SystemAccount.Fees, AccountType.Revenue, true)]
    [InlineData(SystemAccount.BillerSettlement, AccountType.Liability, true)]
    public void System_accounts_have_fixed_types(
        SystemAccount code,
        AccountType expectedType,
        bool expectedCreditNormal)
    {
        var account = LedgerAccount.CreateSystem(code, Currency.PKR, Now);

        account.Type.Should().Be(expectedType);
        account.IsCreditNormal.Should().Be(expectedCreditNormal);
        account.SystemCode.Should().Be(code);
        account.WalletId.Should().BeNull();
    }

    [Fact]
    public void Wallet_account_requires_a_wallet_id()
    {
        var act = () => LedgerAccount.CreateForWallet(Guid.Empty, Currency.PKR, Now);

        act.Should().Throw<LedgerException>();
    }
}
