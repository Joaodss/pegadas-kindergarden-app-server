namespace Pegadas.SharedKernel.Domain;

/// <summary>
/// Row belongs to a school (tenant). Filtered by the <c>Tenant</c> query filter and stamped
/// on insert from the current tenant context.
/// </summary>
public interface ITenantOwned
{
    Guid SchoolId { get; }
}

/// <summary>
/// Row is never physically deleted during retention; <c>Remove()</c> becomes a tombstone.
/// Filtered by the <c>SoftDelete</c> query filter.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
}

/// <summary>
/// Row tracks its last server modification and an optimistic-concurrency version.
/// Both are maintained by the persistence layer; the sync cursor relies on <see cref="ModifiedAt"/>.
/// </summary>
public interface ITrackedEntity
{
    DateTimeOffset ModifiedAt { get; }

    int Version { get; }
}
