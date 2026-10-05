using InventoryApp.Domain.Catalog;

namespace InventoryApp.Tests.Unit;

/// <summary>
/// The SKU rule is the one piece of domain logic in this module with no dependency on anything, so
/// it is tested exhaustively here rather than through HTTP.
/// </summary>
public class SkuFormatTests
{
    private const string LongestAllowed = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";   // exactly 30
    private const string TooLong = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";          // 31

    [Theory]
    [InlineData("ABC")]
    [InlineData("LED-9W-WARM")]
    [InlineData("ABC-123")]         // hyphen-separated groups are the whole point
    [InlineData("SKU123456")]
    [InlineData("A12")]
    public void Accepts_uppercase_alphanumeric_groups(string sku) =>
        Assert.True(Sku.IsValid(sku));

    [Fact]
    public void Accepts_the_boundary_lengths()
    {
        Assert.True(Sku.IsValid(LongestAllowed));
        Assert.False(Sku.IsValid(TooLong));
        Assert.False(Sku.IsValid("A1"));   // 2 characters: below the minimum
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC 123")]                    // spaces are ambiguous to transcribe
    [InlineData("ABC--123")]                   // doubled separator
    [InlineData("-ABC")]
    [InlineData("ABC-")]
    [InlineData("ABC_123")]                    // underscores are not part of the format
    [InlineData("ABC/123")]
    [InlineData("ABC#123")]
    public void Rejects_anything_else(string? sku) =>
        Assert.False(Sku.IsValid(sku));

    [Fact]
    public void Rejects_lower_case_on_the_raw_value()
    {
        // Lower case is not a valid *stored* SKU. It is still accepted from a user, because the
        // service normalises before validating - see Normalize_uppercases_and_trims.
        Assert.False(Sku.IsValid("abc"));
    }

    [Fact]
    public void Normalize_uppercases_and_trims()
    {
        Assert.Equal("LED-9W-WARM", Sku.Normalize("  led-9w-warm "));
        Assert.Equal("ABC123", Sku.Normalize("abc123"));
    }

    [Fact]
    public void A_normalized_lowercase_sku_is_then_valid() =>
        // The behaviour users expect: typing in lower case is accepted, not refused.
        Assert.True(Sku.IsValid(Sku.Normalize("led-9w-warm")));
}