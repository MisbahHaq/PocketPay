namespace InventoryApp.Domain.Entities;

/// <summary>
/// A node in the product taxonomy. Self-referencing through <see cref="ParentCategoryId"/>, which
/// is deliberately limited to one level: deeper trees make every breadcrumb, filter and permission
/// question recursive, and a warehouse catalogue is almost always flat plus one group.
/// </summary>
public class Category
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Null for a root category. A non-null value must itself point at a root category.</summary>
    public Guid? ParentCategoryId { get; set; }

    /// <summary>
    /// Hides a category from pickers without touching the products under it. Unlike a product,
    /// a category is never deleted once it has history - see the catalog service for why.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public Category? ParentCategory { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>True when this category may be used as a parent, i.e. it is not itself a subcategory.</summary>
    public bool CanHaveChildren => ParentCategoryId is null;
}