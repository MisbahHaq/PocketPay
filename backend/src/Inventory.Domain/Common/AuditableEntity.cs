namespace Inventory.Domain.Common;

/// <summary>
/// Marks an entity that records who created/changed it and when. Applied to every
/// table that represents a business decision, because "who approved this purchase
/// order" is a question auditors ask and the ledger alone cannot answer it.
/// </summary>
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id) : base(id)
    {
    }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}