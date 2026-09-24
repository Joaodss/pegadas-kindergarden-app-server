using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pegadas.BuildingBlocks.Persistence;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.BuildingBlocks.Outbox;

/// <summary>
/// Moves domain events raised by tracked entities into the module's outbox table, inside the
/// same <c>SaveChanges</c> (and therefore the same transaction) as the data change.
/// Registered after <see cref="TrackedEntityInterceptor"/> so new rows already carry their tenant.
/// </summary>
internal sealed class OutboxInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
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
        if (context is not ModuleDbContext moduleContext)
        {
            return;
        }

        var raisers = moduleContext.ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        if (raisers.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entity in raisers)
        {
            var schoolId = (entity as ITenantOwned)?.SchoolId ?? moduleContext.CurrentSchoolId;
            foreach (var domainEvent in entity.DomainEvents)
            {
                moduleContext.OutboxMessages.Add(OutboxMessage.From(domainEvent, schoolId, now));
            }

            entity.ClearDomainEvents();
        }
    }
}
