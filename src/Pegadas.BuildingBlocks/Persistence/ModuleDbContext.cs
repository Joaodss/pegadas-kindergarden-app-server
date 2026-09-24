using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pegadas.BuildingBlocks.Outbox;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.BuildingBlocks.Persistence;

/// <summary>
/// Base <see cref="DbContext"/> for a module. Each module owns one schema, and its context:
/// <list type="bullet">
/// <item>maps the module's own <c>outbox_message</c> and <c>processed_message</c> tables;</item>
/// <item>applies the named <see cref="QueryFilters.Tenant"/> and <see cref="QueryFilters.SoftDelete"/> filters by convention;</item>
/// <item>marks <see cref="ITrackedEntity.Version"/> as the optimistic-concurrency token.</item>
/// </list>
/// Contexts are pooled, so they cannot take scoped dependencies: the tenant is assigned to
/// <see cref="CurrentSchoolId"/> by the scoped factory registered in
/// <see cref="PersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}"/>.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Tenant used by the query filter. <c>null</c> makes tenant-owned queries return nothing.</summary>
    public Guid? CurrentSchoolId { get; internal set; }

    /// <summary>The PostgreSQL schema owned by the module.</summary>
    protected abstract string Schema { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedMessageConfiguration());

        ConfigureModel(modelBuilder);

        ApplyConventions(modelBuilder);
    }

    /// <summary>Module-specific mappings (typically <c>ApplyConfigurationsFromAssembly</c>).</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    private void ApplyConventions(ModelBuilder modelBuilder)
    {
        var rootEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && !t.IsOwned())
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in rootEntityTypes)
        {
            var entity = modelBuilder.Entity(clrType);

            if (typeof(Entity).IsAssignableFrom(clrType))
            {
                entity.Ignore(nameof(Entity.DomainEvents));
            }

            if (typeof(ITrackedEntity).IsAssignableFrom(clrType))
            {
                entity.Property(nameof(ITrackedEntity.Version)).IsConcurrencyToken();
            }

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                // No index here: modules define composite indexes led by school_id for their queries.
                entity.HasQueryFilter(QueryFilters.Tenant, BuildTenantFilter(clrType));
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                entity.HasQueryFilter(QueryFilters.SoftDelete, BuildSoftDeleteFilter(clrType));
            }
        }
    }

    // e => (Guid?)e.SchoolId == this.CurrentSchoolId
    // EF Core recognises the context instance and turns CurrentSchoolId into a query parameter.
    private LambdaExpression BuildTenantFilter(Type clrType)
    {
        var e = Expression.Parameter(clrType, "e");
        var schoolId = Expression.Convert(Expression.Property(e, nameof(ITenantOwned.SchoolId)), typeof(Guid?));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentSchoolId));
        return Expression.Lambda(Expression.Equal(schoolId, current), e);
    }

    // e => e.DeletedAt == null
    private static LambdaExpression BuildSoftDeleteFilter(Type clrType)
    {
        var e = Expression.Parameter(clrType, "e");
        var deletedAt = Expression.Property(e, nameof(ISoftDeletable.DeletedAt));
        return Expression.Lambda(Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTimeOffset?))), e);
    }
}
