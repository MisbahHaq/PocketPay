using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Enums;

namespace InventoryApp.Tests.Unit;

/// <summary>
/// The rules the entity itself enforces. Every one of these is also checked by a request validator,
/// so they pass even with the HTTP layer bypassed - which is the point of asserting them here.
/// </summary>
public class ProductTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static Product NewProduct() => new("LED-9W-WARM") { Id = Guid.NewGuid() };

    private static void Update(
        Product product,
        int reorderLevel = 0,
        decimal costPrice = 5m,
        decimal salePrice = 10m) =>
        product.Update(
            "Warm white LED",
            "A bulb.",
            CategoryId,
            UnitOfMeasure.Piece,
            reorderLevel,
            costPrice,
            salePrice,
            isActive: true);

    [Fact]
    public void An_update_applies_every_editable_field()
    {
        var product = NewProduct();

        Update(product);

        Assert.Equal("Warm white LED", product.Name);
        Assert.Equal("A bulb.", product.Description);
        Assert.Equal(CategoryId, product.CategoryId);
        Assert.Equal(UnitOfMeasure.Piece, product.UnitOfMeasure);
        Assert.Equal(5m, product.CostPrice);
        Assert.Equal(10m, product.SalePrice);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void The_sku_survives_an_update()
    {
        var product = NewProduct();

        Update(product);

        Assert.Equal("LED-9W-WARM", product.Sku);
    }

    [Theory]
    [InlineData(0, 5, 10)]        // free item, and a normal margin
    [InlineData(0, 10, 10)]       // sold at cost: allowed, no margin
    [InlineData(0, 0, 0)]         // free of charge
    public void Sensible_pricing_is_accepted(int reorderLevel, decimal costPrice, decimal salePrice)
    {
        var product = NewProduct();

        Update(product, reorderLevel, costPrice, salePrice);

        Assert.Equal(salePrice, product.SalePrice);
    }

    [Fact]
    public void A_negative_reorder_level_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Update(NewProduct(), reorderLevel: -1));

    [Fact]
    public void A_negative_cost_price_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Update(NewProduct(), costPrice: -0.01m));

    [Fact]
    public void A_negative_sale_price_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Update(NewProduct(), costPrice: 0m, salePrice: -1m));

    [Fact]
    public void Selling_below_cost_is_refused()
    {
        // Deliberate in the real world - clearance, a loss leader - which is exactly why it needs
        // saying on purpose rather than happening by accident.
        Assert.Throws<ArgumentException>(() => Update(NewProduct(), costPrice: 10m, salePrice: 9.99m));
    }

    [Fact]
    public void A_refused_update_leaves_the_product_untouched()
    {
        var product = NewProduct();
        Update(product, costPrice: 5m, salePrice: 10m);

        Assert.Throws<ArgumentException>(() => Update(product, costPrice: 20m, salePrice: 1m));

        // Nothing partially applied: a half-written product is worse than a rejected one.
        Assert.Equal(10m, product.SalePrice);
        Assert.Equal(5m, product.CostPrice);
    }

    [Fact]
    public void Soft_deleting_records_when_and_who()
    {
        var product = NewProduct();
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        product.MarkDeleted(now, "user-1");

        Assert.True(product.IsDeleted);
        Assert.Equal(now, product.DeletedAt);
        Assert.Equal("user-1", product.DeletedBy);
    }

    [Fact]
    public void Restoring_clears_the_deletion_fields()
    {
        var product = NewProduct();
        product.MarkDeleted(DateTimeOffset.UtcNow, "user-1");

        product.MarkRestored();

        Assert.False(product.IsDeleted);
        Assert.Null(product.DeletedAt);
        Assert.Null(product.DeletedBy);
    }

    [Fact]
    public void The_sku_cannot_be_changed_from_outside_the_entity()
    {
        // A private setter is what actually enforces immutability; a public one would undo it while
        // still leaving the XML comment claiming otherwise. Reflection is used because the compiler
        // is the primary defence here, and this test exists to stop a future edit quietly removing it.
        var setter = typeof(Product).GetProperty(nameof(Product.Sku))!.SetMethod;

        Assert.NotNull(setter);
        Assert.False(setter!.IsPublic);
    }
}