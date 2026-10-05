using FluentValidation;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Catalog;

namespace InventoryApp.Application.Validators;

/// <summary>
/// Validation for the catalogue write paths.
/// </summary>
/// <remarks>
/// List-query validation is deliberately absent: paging and sorting are clamped in
/// <see cref="ProductQuery.Normalize"/> rather than rejected, because a bad page number in a
/// bookmarked URL should not turn into an error page.
/// </remarks>
public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty()
            .Must(Sku.IsValid)
            .WithMessage($"SKU must be {Sku.Description}");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(1000);

        RuleFor(request => request.CategoryId)
            .NotEmpty()
            .WithMessage("A category is required.");

        RuleFor(request => request.UnitOfMeasure)
            .IsInEnum()
            .WithMessage("Unit of measure is not a recognised value.");

        RuleFor(request => request.ReorderLevel)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Reorder level cannot be negative.");

        RuleFor(request => request.CostPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Cost price cannot be negative.");

        RuleFor(request => request.SalePrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Sale price cannot be negative.");

        // A hard rule rather than a warning. A "warning" channel would mean a warnings envelope on
        // every request and response DTO, and a client is free to ignore it anyway - so it would
        // only be a rule that sometimes does not apply. Under-cost lines are a real requirement
        // (clearance, loss leaders); when that arrives it should be an explicit, named flag on the
        // request, not a soft failure that anyone can switch off.
        RuleFor(request => request.SalePrice)
            .GreaterThanOrEqualTo(request => request.CostPrice)
            .WithMessage("Sale price cannot be lower than cost price.");
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        // No SKU rule: the field is not on the request, so there is nothing to validate and nothing
        // that can be changed. Immutability is enforced by the shape of the type, not by a check.

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(1000);

        RuleFor(request => request.CategoryId)
            .NotEmpty()
            .WithMessage("A category is required.");

        RuleFor(request => request.UnitOfMeasure)
            .IsInEnum()
            .WithMessage("Unit of measure is not a recognised value.");

        RuleFor(request => request.ReorderLevel)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Reorder level cannot be negative.");

        RuleFor(request => request.CostPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Cost price cannot be negative.");

        RuleFor(request => request.SalePrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Sale price cannot be negative.");

        RuleFor(request => request.SalePrice)
            .GreaterThanOrEqualTo(request => request.CostPrice)
            .WithMessage("Sale price cannot be lower than cost price.");

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(version => version.Length > 0)
            .WithMessage("A row version is required so concurrent edits can be detected.");
    }
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Description)
            .MaximumLength(500);

        RuleFor(request => request.ParentCategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("A parent category id cannot be an empty guid.");
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Description)
            .MaximumLength(500);

        RuleFor(request => request.ParentCategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("A parent category id cannot be an empty guid.");
    }
}