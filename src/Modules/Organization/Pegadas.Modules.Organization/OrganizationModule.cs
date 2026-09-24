using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.BuildingBlocks.Modules;

namespace Pegadas.Modules.Organization;

/// <summary>
/// School, rooms, staff and room assignments, children and health profiles, entry types and
/// the per-room quick bar (schema <c>organization</c>). Implemented in Phase 3.
/// </summary>
public sealed class OrganizationModule : IModule
{
    public const string Schema = "organization";

    public string Name => "Organization";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
