using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.BuildingBlocks.Modules;

namespace Pegadas.Modules.Summaries;

/// <summary>
/// Read-only: child history and period summaries over SQL views in schema <c>summaries</c>.
/// Implemented in Phase 6.
/// </summary>
public sealed class SummariesModule : IModule
{
    public const string Schema = "summaries";

    public string Name => "Summaries";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
