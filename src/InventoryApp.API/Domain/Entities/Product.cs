using InventoryApp.Domain.Enums;

namespace InventoryApp.Domain.Entities;

/// <summary>
/// A sellable catalogue item. This is the reference copy of what a product *is*; stock levels and
/// movements live in later modules and will hang off this row by id.
/// </summary>
public class Product
{
    public Guid Id { get; set; }

    /// <summary>
    /// Immutable once assigned. A SKU that identifies a product on paperwork, a barcode and a
    /// customer's purchase order cannot change without silently re-pointing all of them at
    /// something else.
    /// </summary>
    /// <remarks>
    /// Enforced by the property having no setter. Leaving the DTO without a SKU field keeps the
    /// request shape honest, but a rule only a cooperating caller respects is not a rule: any other
    /// code path that got hold of this entity could still reassign it. The constructor below is
    /// EF's, which is why it is private and why the service sets the SKU immediately after
    /// construction - that pairing is the one place the value is assigned.
    /// </remarks>
    public string Sku { get; private set; } = string.Empty;

    /// <summary>
    /// Creates a product with its SKU, which is the only field assigned here: everything else goes
    /// through <see cref="Update"/> so that one set of rules covers both new and existing products.
    /// </summary>
    public Product(string sku)
    {
        Sku = sku;
    }

    /// <summary>Required by EF Core, which materialises entities without going through a constructor.</summary>
    private Product()
    {
    }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public UnitOfMeasure UnitOfMeasure { get; set; }

    /// <summary>Quantity at which the item should be reordered. Zero means "track but do not warn".</summary>
    public int ReorderLevel { get; set; }

    /// <summary>What the business paid. Never returned to Staff, who have no need for it.</summary>
    public decimal CostPrice { get; set; }

    /// <summary>What the customer pays.</summary>
    public decimal SalePrice { get; set; }

    /// <summary>Retired from sale but kept for history. Distinct from <see cref="IsDeleted"/>.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Applies every editable field at once, refusing a combination that is not a sellable product.
    /// Assigning the properties directly would leave the invariants to the caller's discretion, and
    /// this is the class where they belong: a negative price or a sale below cost is not a
    /// temporarily awkward row, it is a row that makes every margin report built on it a lie.
    /// </summary>
    public void Update(
        string name,
        string? description,
        Guid categoryId,
        UnitOfMeasure unitOfMeasure,
        int reorderLevel,
        decimal costPrice,
        decimal salePrice,
        bool isActive)
    {
        EnsureSellable(reorderLevel, costPrice, salePrice);

        Name = name;
        Description = description;
        CategoryId = categoryId;
        UnitOfMeasure = unitOfMeasure;
        ReorderLevel = reorderLevel;
        CostPrice = costPrice;
        SalePrice = salePrice;
        IsActive = isActive;
    }

    /// <summary>
    /// The money and quantity rules a product must satisfy. Stated once here so creation and update
    /// cannot drift apart.
    /// </summary>
    /// <remarks>
    /// Guards the entity, and FluentValidation guards the request. That is duplication on purpose:
    /// the validator's job is to answer "which field was wrong?" for a user, and it can be bypassed
    /// by anything that is not an HTTP call - a seeder, a future import, a test. This one has no
    /// caller to bypass it.
    /// </remarks>
    private static void EnsureSellable(int reorderLevel, decimal costPrice, decimal salePrice)
    {
        if (reorderLevel < 0)
            throw new ArgumentOutOfRangeException(
                nameof(reorderLevel), reorderLevel, "Reorder level cannot be negative.");

        if (costPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(costPrice), costPrice, "Cost price cannot be negative.");

        if (salePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(salePrice), salePrice, "Sale price cannot be negative.");

        // A sale below cost is a deliberate thing to do - clearance, a loss leader - so it is refused
        // here rather than silently allowed, and the caller who means it has to say so explicitly.
        if (salePrice < costPrice)
            throw new ArgumentException("Sale price cannot be below cost price.", nameof(salePrice));
    }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>User id of the creator, or null when seeded by the system.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Equals <see cref="CreatedAt"/> until the row is first edited, so it is never null.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>Soft-delete flag. Products are retired rather than removed: past stock movements and
    /// invoices reference them, and a hard delete would either fail on those or erase the evidence.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    /// <summary>
    /// Opaque token rotated on every write and carried back by a client on update, so two people
    /// editing the same product cannot silently overwrite each other. Set by the DbContext, never
    /// by the entity: its value is only ever meaningful as the thing EF compares against.
    /// </summary>
    public byte[] RowVersion { get; internal set; } = [];

    /// <summary>Applies the audit and soft-delete fields for a deletion in one place.</summary>
    public void MarkDeleted(DateTimeOffset now, string? userId)
    {
        IsDeleted = true;
        DeletedAt = now;
        DeletedBy = userId;
    }

    /// <summary>Reverses a soft delete. Clears the deletion fields rather than nulling the row.</summary>
    public void MarkRestored()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}