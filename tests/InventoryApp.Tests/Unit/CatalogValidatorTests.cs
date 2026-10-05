using InventoryApp.Application.Dtos;
using InventoryApp.Application.Validators;
using InventoryApp.Domain.Enums;

namespace InventoryApp.Tests.Unit;

/// <summary>
/// The create/update validation rules, exercised directly against the validators. These rules are
/// what turn a bad payload into a 400 with a field-named message, so they are worth pinning down
/// without going through HTTP.
/// </summary>
public class CatalogValidatorTests
{
    private static readonly CreateProductRequestValidator CreateValidator = new();
    private static readonly UpdateProductRequestValidator UpdateValidator = new();
    private static readonly CreateCategoryRequestValidator CreateCategoryValidator = new();

    private static CreateProductRequest ValidProduct(Action<CreateProductRequestBuilder>? customise = null)
    {
        var builder = new CreateProductRequestBuilder();
        customise?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public void A_well_formed_product_passes() =>
        Assert.True(CreateValidator.Validate(ValidProduct()).IsValid);

    [Fact]
    public void The_validator_sees_the_raw_sku_because_normalising_is_the_services_job()
    {
        // Stated explicitly so the order is not accidentally reversed later: normalise, then
        // validate. The end-to-end path that relies on it is covered by the integration tests.
        var result = CreateValidator.Validate(ValidProduct(request => request.Sku = "led-9w"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Sku");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("LED-9W-WARM-EXTRA-LONG-SKU-THAT-OVERFLOWS")]
    [InlineData("-LED")]
    [InlineData("LED-")]
    [InlineData("LED 9W")]
    public void A_malformed_sku_is_rejected_with_a_message_naming_the_format(string? sku)
    {
        var result = CreateValidator.Validate(ValidProduct(request => request.Sku = sku!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == "Sku" && error.ErrorMessage.Contains("hyphens"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void A_negative_reorder_level_is_rejected(int reorderLevel)
    {
        var result = CreateValidator.Validate(ValidProduct(request => request.ReorderLevel = reorderLevel));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ReorderLevel");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void A_negative_price_is_rejected(decimal price)
    {
        var cost = CreateValidator.Validate(ValidProduct(r => r.CostPrice = price));
        var sale = CreateValidator.Validate(ValidProduct(r => r.SalePrice = price));

        Assert.Contains(cost.Errors, error => error.PropertyName == "CostPrice");
        Assert.Contains(sale.Errors, error => error.PropertyName == "SalePrice");
    }

    [Fact]
    public void Selling_below_cost_is_rejected()
    {
        var result = CreateValidator.Validate(ValidProduct(r => { r.CostPrice = 10m; r.SalePrice = 9.99m; }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == "SalePrice" && error.ErrorMessage.Contains("lower than cost"));
    }

    [Fact]
    public void Selling_at_cost_is_allowed()
    {
        // Break-even is a legitimate price, so the rule is >= and not >.
        var result = CreateValidator.Validate(ValidProduct(r => { r.CostPrice = 10m; r.SalePrice = 10m; }));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Zero_prices_are_allowed()
    {
        var result = CreateValidator.Validate(ValidProduct(r => { r.CostPrice = 0m; r.SalePrice = 0m; }));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData((UnitOfMeasure)99)]
    [InlineData((UnitOfMeasure)(-1))]
    public void An_unrecognised_unit_of_measure_is_rejected(UnitOfMeasure unit)
    {
        var result = CreateValidator.Validate(ValidProduct(request => request.UnitOfMeasure = unit));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "UnitOfMeasure");
    }

    [Fact]
    public void An_empty_category_id_is_rejected() =>
        Assert.Contains(
            CreateValidator.Validate(ValidProduct(r => r.CategoryId = Guid.Empty)).Errors,
            error => error.PropertyName == "CategoryId");

    [Fact]
    public void An_over_long_name_is_rejected() =>
        Assert.Contains(
            CreateValidator.Validate(ValidProduct(r => r.Name = new string('x', 201))).Errors,
            error => error.PropertyName == "Name");

    [Fact]
    public void Update_requires_a_row_version()
    {
        // Without it the concurrency check silently degrades into a last-write-wins.
        var request = new UpdateProductRequest(
            "Name", null, Guid.NewGuid(), UnitOfMeasure.Piece, 0, 1m, 2m, true, []);

        var result = UpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "RowVersion");
    }

    [Fact]
    public void Update_has_no_sku_rule_because_it_has_no_sku_field()
    {
        // Immutability is enforced by the shape of the DTO: there is no property to set.
        var properties = typeof(UpdateProductRequest).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain("Sku", properties);
        Assert.Contains("RowVersion", properties);
    }

    [Fact]
    public void A_category_name_is_required() =>
        Assert.False(CreateCategoryValidator
            .Validate(new CreateCategoryRequest("", null, null))
            .IsValid);

    [Fact]
    public void An_empty_parent_category_guid_is_rejected() =>
        // Guid.Empty is what an unset or malformed guid arrives as, and it would otherwise pass the
        // "is it a real category" check as a lookup that simply finds nothing.
        Assert.Contains(
            CreateCategoryValidator
                .Validate(new CreateCategoryRequest("TOOLS", null, Guid.Empty))
                .Errors,
            error => error.PropertyName == "ParentCategoryId");

    /// <summary>Fluent tweaks, so each test reads as the one thing it is asserting.</summary>
    private sealed class CreateProductRequestBuilder
    {
        public string Sku { get; set; } = "LED-9W";
        public string Name { get; set; } = "LED Bulb";
        public string? Description { get; set; }
        public Guid CategoryId { get; set; } = Guid.NewGuid();
        public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Piece;
        public int ReorderLevel { get; set; }
        public decimal CostPrice { get; set; } = 5m;
        public decimal SalePrice { get; set; } = 9.99m;
        public bool IsActive { get; set; } = true;

        public CreateProductRequest Build() =>
            new(Sku, Name, Description, CategoryId, UnitOfMeasure, ReorderLevel, CostPrice, SalePrice, IsActive);
    }
}