using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Pegadas.BuildingBlocks.Outbox;
using Pegadas.BuildingBlocks.Persistence;
using Pegadas.BuildingBlocks.Tenancy;
using Pegadas.SharedKernel.Domain;

namespace Pegadas.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A minimal module used to exercise the building blocks (tenant and soft-delete filters,
/// tracking, outbox) against a real PostgreSQL before the real modules exist.
/// </summary>
public sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "test_module";

    public DbSet<Note> Notes => Set<Note>();

    protected override string Schema => SchemaName;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Note>(note =>
        {
            note.ToTable("note");
            note.Property(n => n.Text).HasMaxLength(200);
            note.HasIndex(n => new { n.SchoolId, n.ModifiedAt });
        });
}

public sealed class Note : Entity, ITenantOwned, ISoftDeletable, ITrackedEntity
{
    private Note()
    {
    }

    public Guid SchoolId { get; private set; }

    public string Text { get; private set; } = null!;

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset ModifiedAt { get; private set; }

    public int Version { get; private set; }

    public static Note Create(string text)
    {
        var note = new Note { Text = text };
        note.Raise(new NoteCreated(note.Id));
        return note;
    }

    public void Edit(string text) => Text = text;
}

public sealed record NoteCreated(Guid NoteId) : DomainEvent;

public sealed class NoteCreatedRecorder
{
    public ConcurrentQueue<(Guid NoteId, Guid? SchoolId)> Handled { get; } = new();

    /// <summary>Set to make the handler throw, to exercise retries.</summary>
    public Guid? FailFor { get; set; }
}

internal sealed class NoteCreatedHandler(NoteCreatedRecorder recorder, ITenantContext tenant) : IDomainEventHandler<NoteCreated>
{
    public Task HandleAsync(NoteCreated domainEvent, CancellationToken cancellationToken)
    {
        if (recorder.FailFor == domainEvent.NoteId)
        {
            throw new InvalidOperationException("Simulated handler failure.");
        }

        recorder.Handled.Enqueue((domainEvent.NoteId, tenant.SchoolId));
        return Task.CompletedTask;
    }
}
