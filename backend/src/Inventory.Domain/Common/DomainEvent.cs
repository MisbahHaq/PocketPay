namespace Inventory.Domain.Common;

/// <summary>Something notable that happened in the domain.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Convenience base that stamps the occurrence time.</summary>
public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent() => OccurredAt = DateTimeOffset.UtcNow;

    protected DomainEvent(DateTimeOffset occurredAt) => OccurredAt = occurredAt;

    public DateTimeOffset OccurredAt { get; init; }
}