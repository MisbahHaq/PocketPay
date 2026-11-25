using FluentAssertions;
using FsCheck.Xunit;
using WalletPlatform.Application.Reconciliation;

namespace WalletPlatform.UnitTests.Reconciliation;

public class ReconciliationPropertyTests
{
    [Property]
    public bool Matched_requires_equal_amounts_single_success_row_and_our_payment(long amount)
    {
        var category = ReconciliationMatcher.Categorize(
            hasOurPayment: true,
            hasBillerRows: true,
            ourAmount: amount,
            billerAmount: amount,
            billerStatus: "Success",
            billerRowCount: 1);

        return category == ReconciliationMatcher.Matched;
    }

    [Property]
    public bool Any_amount_difference_is_always_a_mismatch(long ourAmount, long billerAmount)
    {
        if (ourAmount == billerAmount)
        {
            return true;
        }

        var category = ReconciliationMatcher.Categorize(
            true, true, ourAmount, billerAmount, "Success", 1);
        return category == ReconciliationMatcher.AmountMismatch;
    }

    [Property]
    public bool Missing_side_is_classified_independently_of_amounts(long ourAmount, long billerAmount)
    {
        var missingAtBiller = ReconciliationMatcher.Categorize(
            true, false, ourAmount, billerAmount, null, 0);
        var unknownAtOurSide = ReconciliationMatcher.Categorize(
            false, true, ourAmount, billerAmount, "Success", 1);

        return missingAtBiller == ReconciliationMatcher.MissingAtBiller &&
               unknownAtOurSide == ReconciliationMatcher.UnknownAtOurSide;
    }

    [Property]
    public bool Duplicate_biller_rows_never_match(long amount)
    {
        var category = ReconciliationMatcher.Categorize(
            true, true, amount, amount, "Success", 2);
        return category == ReconciliationMatcher.AmountMismatch;
    }

    [Fact]
    public void Non_success_biller_status_is_a_mismatch()
    {
        ReconciliationMatcher.Categorize(true, true, 100, 100, "Failed", 1)
            .Should().Be(ReconciliationMatcher.AmountMismatch);
        ReconciliationMatcher.Categorize(true, true, 100, 100, "success", 1)
            .Should().Be(ReconciliationMatcher.Matched);
    }
}
