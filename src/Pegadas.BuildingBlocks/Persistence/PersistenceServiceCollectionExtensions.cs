using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Pegadas.BuildingBlocks.Outbox;
using Pegadas.BuildingBlocks.Tenancy;

namespace Pegadas.BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Pegadas";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Registers the shared <see cref="NpgsqlDataSource"/> (one connection pool for EF, raw SQL and health checks).</summary>
    internal static IServiceCollection AddPegadasPersistence(this IServiceCollection services)
    {
        services.TryAddSingleton(sp =>
            NpgsqlDataSource.Create(GetConnectionString(sp.GetRequiredService<IConfiguration>())));

        services.TryAddSingleton<TrackedEntityInterceptor>();
        services.TryAddSingleton<OutboxInterceptor>();

        services.AddHealthChecks().AddCheck<NpgsqlHealthCheck>("postgres", tags: ["ready"]);
        return services;
    }

    /// <summary>
    /// The configured connection string with the app's defaults: an application name (visible
    /// in <c>pg_stat_activity</c>) and GSS encryption off. Pegadas authenticates with a password
    /// or an Entra token, never Kerberos, and probing GSS fails noisily in the chiseled image,
    /// which has no libgssapi. Explicit settings in the configured string win.
    /// </summary>
    internal static string GetConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is not configured.");
        }

        var builder = new NpgsqlConnectionStringBuilder(configured);
        builder.ApplicationName ??= "pegadas-api";
        if (!configured.Contains("GSS", StringComparison.OrdinalIgnoreCase))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Registers a module <see cref="DbContext"/>: pooled, bound to the module schema, with the
    /// persistence interceptors, and resolvable as a scoped service carrying the request's tenant.
    /// Also registers the module's outbox processor.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        services.AddPooledDbContextFactory<TContext>((sp, options) => options
            .UseNpgsql(
                sp.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<TrackedEntityInterceptor>(),
                sp.GetRequiredService<OutboxInterceptor>()));

        // Pooled contexts cannot take scoped dependencies, so the tenant is assigned on rent.
        services.AddScoped(sp =>
        {
            var context = sp.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext();
            context.CurrentSchoolId = sp.GetRequiredService<ITenantContext>().SchoolId;
            return context;
        });

        services.AddHostedService<OutboxProcessor<TContext>>();
        return services;
    }

    /// <summary>
    /// Options for <c>IDesignTimeDbContextFactory</c> implementations (<c>dotnet ef</c>). The
    /// connection string comes from <c>PEGADAS_DESIGN_CONNECTION</c>, defaulting to the local
    /// docker-compose database.
    /// </summary>
    public static DbContextOptions<TContext> DesignTimeOptions<TContext>(string schema)
        where TContext : ModuleDbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                Environment.GetEnvironmentVariable("PEGADAS_DESIGN_CONNECTION")
                    ?? "Host=localhost;Port=5432;Database=pegadas;Username=pegadas;Password=pegadas_dev",
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention()
            .Options;
}

internal sealed class NpgsqlHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", ex);
        }
    }
}
