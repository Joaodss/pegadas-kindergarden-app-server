using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.BuildingBlocks.Outbox;

/// <summary>
/// Handles a domain event taken from the outbox. Delivery is at-least-once: handlers must be
/// idempotent (the outbox skips handlers that already committed, but a crash between the
/// handler's side effect and the commit replays it).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Established name for domain event handlers; not a .NET event delegate.")]
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Resolves and invokes the handlers of an event type without per-call reflection.</summary>
internal sealed class DomainEventDispatcher
{
    private static readonly MethodInfo InvokeMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(InvokeTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly ConcurrentDictionary<Type, (Type HandlerType, Func<object, IDomainEvent, CancellationToken, Task> Invoke)> _cache = new();

    public IReadOnlyList<EventHandlerInvocation> GetHandlers(IServiceProvider services, Type eventType)
    {
        var (handlerType, invoke) = _cache.GetOrAdd(eventType, static type =>
        {
            var closedHandler = typeof(IDomainEventHandler<>).MakeGenericType(type);
            var invoker = InvokeMethod.MakeGenericMethod(type)
                .CreateDelegate<Func<object, IDomainEvent, CancellationToken, Task>>();
            return (closedHandler, invoker);
        });

        return services.GetServices(handlerType)
            .OfType<object>()
            .Select(handler => new EventHandlerInvocation(handler.GetType().FullName!, (e, ct) => invoke(handler, e, ct)))
            .ToList();
    }

    private static Task InvokeTyped<TEvent>(object handler, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent =>
        ((IDomainEventHandler<TEvent>)handler).HandleAsync((TEvent)domainEvent, cancellationToken);
}

internal sealed record EventHandlerInvocation(string Name, Func<IDomainEvent, CancellationToken, Task> Invoke);
