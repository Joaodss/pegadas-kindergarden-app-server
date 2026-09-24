using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegadas.BuildingBlocks.Persistence;
using Pegadas.BuildingBlocks.Security;

namespace Pegadas.BuildingBlocks.Jobs;

internal static class JobsServiceCollectionExtensions
{
    public const string HangfireSchema = "hangfire";

    /// <summary>
    /// Hangfire with PostgreSQL storage for cron and fire-and-forget jobs. The client (enqueueing)
    /// is always available; the server only runs when <c>Workers:Enabled</c> is true. Storage is
    /// created lazily, so processes that never touch Hangfire never connect for it.
    /// </summary>
    public static IServiceCollection AddPegadasJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var workers = configuration.GetSection(WorkersOptions.SectionName).Get<WorkersOptions>() ?? new WorkersOptions();

        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                bootstrap => bootstrap.UseNpgsqlConnection(
                    PersistenceServiceCollectionExtensions.GetConnectionString(sp.GetRequiredService<IConfiguration>())),
                new PostgreSqlStorageOptions
                {
                    SchemaName = HangfireSchema,
                    PrepareSchemaIfNecessary = workers.PrepareSchema,
                    QueuePollInterval = TimeSpan.FromSeconds(5),
                }));

        if (workers.Enabled)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = workers.WorkerCount;
                options.ServerName = $"{Environment.MachineName}:{Environment.ProcessId}";
            });
        }

        return services;
    }
}

/// <summary>
/// Dashboard access: local requests in Development, otherwise an authenticated coordinator.
/// The dashboard itself is only mapped when <c>Ops:HangfireDashboard</c> is true.
/// </summary>
internal sealed class HangfireDashboardAuthorizationFilter(IHostEnvironment environment) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (environment.IsDevelopment() && httpContext.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return httpContext.User.IsInRole(PegadasRoles.Coordinator);
    }
}
