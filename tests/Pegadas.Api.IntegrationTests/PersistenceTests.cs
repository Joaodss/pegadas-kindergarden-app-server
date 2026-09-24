using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegadas.Api.IntegrationTests.Infrastructure;
using Pegadas.BuildingBlocks.Outbox;
using Pegadas.BuildingBlocks.Persistence;
using Pegadas.BuildingBlocks.Tenancy;

namespace Pegadas.Api.IntegrationTests;

/// <summary>Building blocks against a real PostgreSQL: tenant isolation, tombstones, tracking and the outbox.</summary>
public sealed class PersistenceTests(PegadasApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_rows_are_stamped_with_the_tenant_and_tracked()
    {
        var school = Guid.CreateVersion7();

        var note = await AddNoteAsync(school, "first");

        await using var scope = Scope(school);
        var stored = await Db(scope).Notes.SingleAsync(n => n.Id == note.Id, Ct);
        Assert.Equal(school, stored.SchoolId);
        Assert.Equal(1, stored.Version);
        Assert.NotEqual(default, stored.ModifiedAt);
    }

    [Fact]
    public async Task Queries_only_see_rows_of_the_current_tenant()
    {
        var schoolA = Guid.CreateVersion7();
        var schoolB = Guid.CreateVersion7();
        var note = await AddNoteAsync(schoolA, "belongs to A");

        await using (var scopeB = Scope(schoolB))
        {
            Assert.Null(await Db(scopeB).Notes.FirstOrDefaultAsync(n => n.Id == note.Id, Ct));
        }

        await using (var noTenant = Scope(null))
        {
            Assert.False(await Db(noTenant).Notes.AnyAsync(n => n.Id == note.Id, Ct));
        }

        await using var scopeA = Scope(schoolA);
        Assert.NotNull(await Db(scopeA).Notes.FirstOrDefaultAsync(n => n.Id == note.Id, Ct));
    }

    [Fact]
    public async Task Writing_a_row_of_another_tenant_is_refused()
    {
        var schoolA = Guid.CreateVersion7();
        var note = await AddNoteAsync(schoolA, "belongs to A");

        await using var scopeB = Scope(Guid.CreateVersion7());
        var db = Db(scopeB);
        var foreign = await db.Notes.IgnoreQueryFilters([QueryFilters.Tenant]).SingleAsync(n => n.Id == note.Id, Ct);
        foreign.Edit("tampered");

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Updates_bump_the_version_and_deletes_become_tombstones()
    {
        var school = Guid.CreateVersion7();
        var note = await AddNoteAsync(school, "draft");

        await using (var scope = Scope(school))
        {
            var db = Db(scope);
            var stored = await db.Notes.SingleAsync(n => n.Id == note.Id, Ct);
            stored.Edit("final");
            await db.SaveChangesAsync(Ct);
            Assert.Equal(2, stored.Version);

            db.Notes.Remove(stored);
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = Scope(school))
        {
            var db = Db(scope);
            Assert.False(await db.Notes.AnyAsync(n => n.Id == note.Id, Ct));

            var tombstone = await db.Notes.IgnoreQueryFilters([QueryFilters.SoftDelete]).SingleAsync(n => n.Id == note.Id, Ct);
            Assert.NotNull(tombstone.DeletedAt);
            Assert.Equal(3, tombstone.Version);
        }
    }

    [Fact]
    public async Task Concurrent_updates_are_detected()
    {
        var school = Guid.CreateVersion7();
        var note = await AddNoteAsync(school, "v1");

        await using var first = Scope(school);
        await using var second = Scope(school);
        var a = await Db(first).Notes.SingleAsync(n => n.Id == note.Id, Ct);
        var b = await Db(second).Notes.SingleAsync(n => n.Id == note.Id, Ct);

        a.Edit("from first");
        await Db(first).SaveChangesAsync(Ct);

        b.Edit("from second");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Db(second).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Domain_events_go_through_the_outbox_once_with_the_tenant_in_scope()
    {
        var school = Guid.CreateVersion7();
        var note = await AddNoteAsync(school, "with event");
        var recorder = fixture.Factory.Services.GetRequiredService<NoteCreatedRecorder>();

        await using (var scope = Scope(school))
        {
            var message = await Db(scope).OutboxMessages.SingleAsync(m => m.SchoolId == school, Ct);
            Assert.Null(message.ProcessedAt);
            Assert.Contains(nameof(NoteCreated), message.Type, StringComparison.Ordinal);
        }

        await DrainOutboxAsync();
        await DrainOutboxAsync();

        Assert.Single(recorder.Handled, h => h.NoteId == note.Id);
        Assert.Equal(school, recorder.Handled.Single(h => h.NoteId == note.Id).SchoolId);

        await using (var scope = Scope(school))
        {
            var message = await Db(scope).OutboxMessages.SingleAsync(m => m.SchoolId == school, Ct);
            Assert.NotNull(message.ProcessedAt);
            Assert.True(await Db(scope).ProcessedMessages.AnyAsync(p => p.MessageId == message.Id, Ct));
        }
    }

    [Fact]
    public async Task Failing_handlers_are_retried_later_with_backoff()
    {
        var school = Guid.CreateVersion7();
        var recorder = fixture.Factory.Services.GetRequiredService<NoteCreatedRecorder>();
        var noteId = Guid.Empty;

        await using (var scope = Scope(school))
        {
            var note = Note.Create("will fail");
            noteId = note.Id;
            recorder.FailFor = noteId;
            Db(scope).Notes.Add(note);
            await Db(scope).SaveChangesAsync(Ct);
        }

        try
        {
            await DrainOutboxAsync();
        }
        finally
        {
            recorder.FailFor = null;
        }

        await using var check = Scope(school);
        var message = await Db(check).OutboxMessages.SingleAsync(m => m.SchoolId == school, Ct);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.Attempts);
        Assert.NotNull(message.NextAttemptAt);
        Assert.Contains("Simulated handler failure", message.LastError, StringComparison.Ordinal);
        Assert.DoesNotContain(recorder.Handled, h => h.NoteId == noteId);
    }

    private async Task<Note> AddNoteAsync(Guid school, string text)
    {
        await using var scope = Scope(school);
        var note = Note.Create(text);
        Db(scope).Notes.Add(note);
        await Db(scope).SaveChangesAsync(Ct);
        return note;
    }

    private async Task DrainOutboxAsync()
    {
        var processor = fixture.Factory.Services.GetServices<IHostedService>()
            .OfType<OutboxProcessor<TestModuleDbContext>>()
            .Single();

        while (await processor.ProcessBatchAsync(Ct) > 0)
        {
        }
    }

    private AsyncServiceScope Scope(Guid? school)
    {
        var scope = fixture.Factory.Services.CreateAsyncScope();
        if (school is { } id)
        {
            scope.ServiceProvider.GetRequiredService<ITenantSetter>().SetTenant(id);
        }

        return scope;
    }

    private static TestModuleDbContext Db(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
}
