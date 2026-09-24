namespace Pegadas.SharedKernel.Domain;

/// <summary>
/// Base class for entities. Ids are UUIDv7 so they sort by creation time and can be
/// generated on the client (offline-first).
/// </summary>
public abstract class Entity
{
    private List<IDomainEvent>? _domainEvents;

    protected Entity()
        : this(Guid.CreateVersion7())
    {
    }

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; private set; }

    /// <summary>Events raised since the entity was loaded; written to the outbox on save.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents ?? (IReadOnlyCollection<IDomainEvent>)[];

    protected void Raise(IDomainEvent domainEvent) => (_domainEvents ??= []).Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents?.Clear();
}
