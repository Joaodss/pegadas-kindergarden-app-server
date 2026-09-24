namespace Pegadas.SharedKernel.Domain;

/// <summary>
/// Something that happened in the domain (e.g. <c>DayClosed</c>). Events are named in the
/// past tense, carry ids only (no personal data) and are persisted through the outbox.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
}

/// <summary>Convenience base record for domain events.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
