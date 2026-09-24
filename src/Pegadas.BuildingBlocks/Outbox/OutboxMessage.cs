using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.BuildingBlocks.Outbox;

/// <summary>
/// A domain event persisted in the same transaction as the change that raised it
/// (table <c>&lt;module schema&gt;.outbox_message</c>).
/// </summary>
public sealed class OutboxMessage
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const int MaxErrorLength = 2000;

    private OutboxMessage()
    {
    }

    /// <summary>Equal to the event's <see cref="IDomainEvent.EventId"/>.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant the event belongs to; handlers run with this tenant in scope.</summary>
    public Guid? SchoolId { get; private set; }

    /// <summary>
    /// "Full.Type.Name, Assembly". Event types are contracts: renaming one strands its
    /// unprocessed messages.
    /// </summary>
    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    internal static OutboxMessage From(IDomainEvent domainEvent, Guid? schoolId, DateTimeOffset now)
    {
        var type = domainEvent.GetType();
        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            SchoolId = schoolId,
            Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
            Payload = JsonSerializer.Serialize(domainEvent, type, SerializerOptions),
            OccurredAt = now,
        };
    }

    internal void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
        NextAttemptAt = null;
    }

    /// <summary>Records a failure and schedules the retry with exponential backoff (2^n s, capped at 1 h).</summary>
    internal void MarkFailed(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        NextAttemptAt = now.AddSeconds(Math.Min(Math.Pow(2, Attempts), 3600));
    }
}

/// <summary>Records that a handler has processed an outbox message, so retries do not run it again.</summary>
public sealed class ProcessedMessage
{
    private ProcessedMessage()
    {
    }

    internal ProcessedMessage(Guid messageId, string handler, DateTimeOffset processedAt)
    {
        MessageId = messageId;
        Handler = handler;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }

    public string Handler { get; private set; } = null!;

    public DateTimeOffset ProcessedAt { get; private set; }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_message");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(500);
        builder.Property(m => m.Payload).HasColumnType("jsonb");

        // Only pending rows are indexed; processed rows are kept for troubleshooting and purged by a job.
        builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL");
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_message");
        builder.HasKey(p => new { p.MessageId, p.Handler });
        builder.Property(p => p.Handler).HasMaxLength(500);
    }
}
