using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// Development-only sample catalogue. Gives a reviewer enough rows to page, search and sort
/// against without typing any data in.
/// </summary>
/// <remarks>
/// Gated on the environment rather than on a configuration flag on purpose: the flag could be
/// switched on in production by accident, whereas "not Development" cannot.
/// </remarks>
internal sealed class CatalogSeeder(ApplicationDbContext dbContext, ILogger<CatalogSeeder> logger)
{
    private const int ProductsPerCategory = 6;

    /// <summary>Deterministic, so a second run produces the same catalogue and a diff is readable.</summary>
    private static readonly string[] Nouns =
    [
        "Standard", "Heavy Duty", "Compact", "Industrial", "Precision", "Eco",
        "Reinforced", "Portable", "High Flow", "Low Profile", "Twin Pack", "Professional"
    ];

    private static readonly string[] Qualifiers =
    [
        "Unit", "Kit", "Pack", "Refill", "Spare", "Set",
        "Extension", "Adapter", "Casing", "Filter", "Bracket", "Cable"
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Categories.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Catalog sample data already present; skipping.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var categories = new List<Category>();
        var products = new List<Product>();

        // Five roots, each with one subcategory, so both levels of the tree and the "filter by parent
        // includes children" behaviour are exercisable straight away.
        var roots = new (string Name, string Description, string ChildName)[]
        {
            ("ELECTRONICS", "Consumer and industrial electronic components.", "PASSIVES"),
            ("HARDWARE", "Fixings, fasteners and structural ironmongery.", "FASTENERS"),
            ("SAFETY", "Personal protective equipment and site safety gear.", "PPE"),
            ("CLEANING", " janitorial and hygiene supplies.", "JANITORIAL"),
            ("PACKAGING", "Boxes, labels and protective wrap.", "CONSUMABLES")
        };

        foreach (var (name, description, childName) in roots)
        {
            var root = new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                IsActive = true
            };

            var child = new Category
            {
                Id = Guid.NewGuid(),
                Name = $"{name[..Math.Min(4, name.Length)]}-{childName}",
                Description = $"{childName.ToLowerInvariant()} within {name.ToLowerInvariant()}.",
                ParentCategoryId = root.Id,
                IsActive = true
            };

            categories.Add(root);
            categories.Add(child);

            foreach (var product in BuildProducts(root, child, now))
                products.Add(product);
        }

        dbContext.Categories.AddRange(categories);
        dbContext.Products.AddRange(products);

        // Save through the context rather than per-entity inserts: one transaction, and the audit
        // override stamps every row consistently.
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {CategoryCount} categories and {ProductCount} products for development.",
            categories.Count,
            products.Count);
    }

    /// <summary>
    /// Deterministic pseudo-randomness from an integer seed rather than <see cref="Random"/>.
    /// </summary>
    /// <remarks>
    /// A seeder that produces different rows on every run makes it impossible to tell a data bug from
    /// new data, and impossible to reproduce a bug someone else reported.
    /// </remarks>
    private static IEnumerable<Product> BuildProducts(Category root, Category child, DateTimeOffset now)
    {
        for (var index = 0; index < ProductsPerCategory; index++)
        {
            var category = index % 2 == 0 ? root : child;
            var noun = Nouns[Math.Abs(Hash(root.Name, index)) % Nouns.Length];
            var qualifier = Qualifiers[Math.Abs(Hash(child.Name, index)) % Qualifiers.Length];

            var cost = Round(1m + (Math.Abs(Hash(root.Name, index * 7)) % 40_000) / 100m);

            var product = new Product($"{Truncate(root.Name, 4)}-{Truncate(child.Name, 6)}-{index + 1:D2}")
            {
                Id = Guid.NewGuid()
            };

            // Through Update so the seeded rows meet the same rules as any entered by hand.
            product.Update(
                $"{noun} {qualifier}",
                $"{noun} {qualifier} filed under {category.Name.ToLowerInvariant()}.",
                category.Id,
                (UnitOfMeasure)(index % Enum.GetValues<UnitOfMeasure>().Length),
                5 * (index % 6),
                cost,
                // Always above cost, as the domain rule requires.
                Round(cost * 1.35m),
                // One in five inactive, so ?isActive=false has something to return.
                isActive: index % 5 != 0);

            // Spread creation dates over the past year so date sorting and range filtering are
            // meaningful rather than all-equal. The DbContext would stamp these on save, but it
            // would stamp them all "now", which defeats the point of seeding varied dates.
            product.CreatedAt = now.AddDays(-(index * 37));
            product.UpdatedAt = product.CreatedAt;

            yield return product;
        }
    }

    private static int Hash(string value, int seed) => string.GetHashCode(value, StringComparison.Ordinal) + seed;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);

    /// <summary>Keeps generated SKUs inside the 30-character limit.</summary>
    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}