using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.BuildingBlocks.Persistence;

/// <summary>
/// Applies the persistence conventions of the domain markers before every save:
/// <list type="bullet">
/// <item><see cref="ITenantOwned"/>: stamps <c>SchoolId</c> on insert and refuses to write rows of another tenant;</item>
/// <item><see cref="ISoftDeletable"/>: turns deletes into tombstones (<c>DeletedAt</c>);</item>
/// <item><see cref="ITrackedEntity"/>: sets <c>ModifiedAt</c> and bumps <c>Version</c> on update.</item>
/// </list>
/// Singleton: the tenant is read from the context being saved, not from DI.
/// </summary>
internal sealed class TrackedEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var tenant = (context as ModuleDbContext)?.CurrentSchoolId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity is ITenantOwned owned)
            {
                EnforceTenant(entry, owned, tenant);
            }

            if (entry.Entity is ITrackedEntity)
            {
                entry.Property(nameof(ITrackedEntity.ModifiedAt)).CurrentValue = now;

                var version = entry.Property(nameof(ITrackedEntity.Version));
                if (entry.State == EntityState.Added && (int)version.CurrentValue! < 1)
                {
                    version.CurrentValue = 1;
                }
                else if (entry.State == EntityState.Modified && Equals(version.CurrentValue, version.OriginalValue))
                {
                    // Bump unless the caller set the version explicitly (e.g. a sync upsert).
                    version.CurrentValue = (int)version.CurrentValue! + 1;
                }
            }
        }
    }

    private static void EnforceTenant(EntityEntry entry, ITenantOwned owned, Guid? tenant)
    {
        if (entry.State == EntityState.Added && owned.SchoolId == Guid.Empty)
        {
            entry.Property(nameof(ITenantOwned.SchoolId)).CurrentValue = tenant
                ?? throw new InvalidOperationException(
                    $"Cannot insert {entry.Metadata.ClrType.Name} without a tenant in scope.");
            return;
        }

        if (tenant is { } current && owned.SchoolId != current)
        {
            throw new InvalidOperationException(
                $"Refusing to write a {entry.Metadata.ClrType.Name} that belongs to another tenant.");
        }
    }
}
