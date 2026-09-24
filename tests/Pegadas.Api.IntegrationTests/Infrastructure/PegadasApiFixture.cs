using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.Api.IntegrationTests.Infrastructure;
using Pegadas.BuildingBlocks.Outbox;
using Pegadas.BuildingBlocks.Persistence;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PegadasApiFixture))]

namespace Pegadas.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container and one API host for the whole test assembly. Tests isolate their
/// data by using fresh school (tenant) ids rather than resetting the database.
/// Requires Docker.
/// </summary>
public sealed class PegadasApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("pegadas")
        .WithUsername("pegadas")
        .WithPassword("pegadas_test")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public HttpClient CreateClient() => Factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new PegadasApiFactory(_postgres.GetConnectionString());

        // Create the tables of the test module (migrations arrive with the real modules).
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    private sealed class PegadasApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Pegadas", connectionString);

            // Background loops off: tests drive the outbox processor explicitly.
            builder.UseSetting("Workers:Enabled", "false");

            builder.ConfigureTestServices(services =>
            {
                services.AddModuleDbContext<TestModuleDbContext>(TestModuleDbContext.SchemaName);
                services.AddSingleton<NoteCreatedRecorder>();
                services.AddScoped<IDomainEventHandler<NoteCreated>, NoteCreatedHandler>();
            });
        }
    }
}
