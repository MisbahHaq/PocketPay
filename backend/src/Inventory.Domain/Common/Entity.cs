namespace Inventory.Domain.Common;

/// <summary>
/// Base class for every persisted aggregate.
/// </summary>
/// <remarks>
/// Ids are generated in the constructor rather than by the database. That means an
/// entity is fully formed and safe to pass around (or to a domain event handler)
/// the instant it is created, and it makes inserts independent of a round trip.
/// </remarks>
public abstract class Entity
{
    protected Entity() => Id = SequentialGuid.New();

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; protected set; }

    /// <summary>
    /// Domain events raised by this entity during the current unit of work.
    /// </summary>
    /// <remarks>
    /// They are dispatched <em>after</em> the transaction commits, never before.
    /// Publishing inside the transaction means an event handler that throws would
    /// roll back work the caller was told succeeded, and a handler that succeeds
    /// but whose commit then fails would have already told the world about a change
    /// that does not exist.
    /// </remarks>
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}