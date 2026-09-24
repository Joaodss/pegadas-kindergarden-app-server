using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pegadas.BuildingBlocks.Jobs;
using Pegadas.BuildingBlocks.Observability;
using Pegadas.BuildingBlocks.Persistence;
using Pegadas.BuildingBlocks.Tenancy;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.BuildingBlocks.Outbox;

/// <summary>
/// Polls one module's outbox and dispatches each pending message to its in-process handlers
/// (ADR-0007). Rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>, so several instances can
/// run side by side without double processing. Runs only when <c>Workers:Enabled</c> is true.
/// </summary>
internal sealed class OutboxProcessor<TContext>(
    IDbContextFactory<TContext> contextFactory,
    IServiceScopeFactory scopeFactory,
    DomainEventDispatcher dispatcher,
    IOptions<OutboxOptions> outboxOptions,
    IOptions<WorkersOptions> workersOptions,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    private static readonly ConcurrentDictionary<string, Type?> EventTypes = new();
    private static readonly string ModuleName = typeof(TContext).Name;

    private string? _claimSql;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!workersOptions.Value.Enabled)
        {
            return;
        }

        var options = outboxOptions.Value;
        using var timer = new PeriodicTimer(options.PollingInterval, timeProvider);
        try
        {
            do
            {
                try
                {
                    // Drain full batches back to back; wait for the next tick once the outbox is empty.
                    while (await ProcessBatchAsync(stoppingToken) == options.BatchSize)
                    {
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    OutboxLog.BatchFailed(logger, ModuleName, ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>Claims and processes one batch in a single transaction. Returns the number of messages claimed.</summary>
    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var options = outboxOptions.Value;

        // Outbox rows are not tenant-owned, so a tenant-less context from the pooled factory is fine here.
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var messages = await context.OutboxMessages
            .FromSqlRaw(GetClaimSql(context), options.MaxAttempts, now, options.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            await ProcessMessageAsync(context, message, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private async Task ProcessMessageAsync(TContext context, OutboxMessage message, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var eventType = EventTypes.GetOrAdd(message.Type, ResolveEventType);
        if (eventType is null)
        {
            Fail(message, $"Unknown domain event type '{message.Type}'.", now, null);
            return;
        }

        IDomainEvent domainEvent;
        try
        {
            domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, eventType, OutboxMessage.SerializerOptions)!;
        }
        catch (JsonException ex)
        {
            Fail(message, $"Payload does not deserialize as {eventType.Name}: {ex.Message}", now, ex);
            return;
        }

        var alreadyProcessed = message.Attempts == 0
            ? []
            : (await context.ProcessedMessages
                .Where(p => p.MessageId == message.Id)
                .Select(p => p.Handler)
                .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        await using var scope = scopeFactory.CreateAsyncScope();
        if (message.SchoolId is { } schoolId)
        {
            scope.ServiceProvider.GetRequiredService<ITenantSetter>().SetTenant(schoolId);
        }

        using var activity = PegadasTelemetry.ActivitySource.StartActivity($"outbox {eventType.Name}", ActivityKind.Consumer);
        activity?.SetTag("pegadas.module", ModuleName);
        activity?.SetTag("pegadas.outbox.message_id", message.Id);

        try
        {
            foreach (var handler in dispatcher.GetHandlers(scope.ServiceProvider, eventType))
            {
                if (alreadyProcessed.Contains(handler.Name))
                {
                    continue;
                }

                await handler.Invoke(domainEvent, cancellationToken);
                context.ProcessedMessages.Add(new ProcessedMessage(message.Id, handler.Name, timeProvider.GetUtcNow()));
            }

            message.MarkProcessed(timeProvider.GetUtcNow());
            PegadasTelemetry.OutboxProcessed.Add(1, new KeyValuePair<string, object?>("pegadas.module", ModuleName));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            Fail(message, $"{ex.GetType().Name}: {ex.Message}", now, ex);
        }
    }

    private void Fail(OutboxMessage message, string error, DateTimeOffset now, Exception? exception)
    {
        message.MarkFailed(error, now);
        PegadasTelemetry.OutboxFailed.Add(1, new KeyValuePair<string, object?>("pegadas.module", ModuleName));
        OutboxLog.MessageFailed(logger, message.Id, ModuleName, message.Attempts, exception);
    }

    private string GetClaimSql(TContext context) => _claimSql ??= BuildClaimSql(context);

    private static string BuildClaimSql(TContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException($"{typeof(TContext).Name} does not map {nameof(OutboxMessage)}.");

        // Identifiers come from the EF model (code constants), never from input. Column names
        // follow the snake_case naming convention used by every module context.
        var table = $"\"{entityType.GetSchema()}\".\"{entityType.GetTableName()}\"";
        return $$"""
            SELECT * FROM {{table}}
            WHERE processed_at IS NULL
              AND attempts < {0}
              AND (next_attempt_at IS NULL OR next_attempt_at <= {1})
            ORDER BY occurred_at
            LIMIT {2}
            FOR UPDATE SKIP LOCKED
            """;
    }

    private static Type? ResolveEventType(string typeName)
    {
        var type = Type.GetType(typeName, throwOnError: false);
        return type is not null && typeof(IDomainEvent).IsAssignableFrom(type) ? type : null;
    }
}

internal static partial class OutboxLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed for {Module}")]
    public static partial void BatchFailed(ILogger logger, string module, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} of {Module} failed (attempt {Attempt})")]
    public static partial void MessageFailed(ILogger logger, Guid messageId, string module, int attempt, Exception? exception);
}
