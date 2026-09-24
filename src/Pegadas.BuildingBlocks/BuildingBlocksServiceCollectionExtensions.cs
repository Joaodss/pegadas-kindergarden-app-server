using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegadas.BuildingBlocks.Jobs;
using Pegadas.BuildingBlocks.Outbox;
using Pegadas.BuildingBlocks.Persistence;
using Pegadas.BuildingBlocks.Security;
using Pegadas.BuildingBlocks.Tenancy;

namespace Pegadas.BuildingBlocks;

public static class BuildingBlocksServiceCollectionExtensions
{
    /// <summary>Registers the shared infrastructure every module relies on.</summary>
    public static IServiceCollection AddPegadasBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentActor, CurrentActor>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton<JwtSigningKeyProvider>();

        services.AddOptions<WorkersOptions>()
            .Bind(configuration.GetSection(WorkersOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<DomainEventDispatcher>();
        services.TryAddSingleton<HangfireDashboardAuthorizationFilter>();

        services.AddPegadasPersistence();
        services.AddPegadasJobs(configuration);

        return services;
    }
}
