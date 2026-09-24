using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.BuildingBlocks.Modules;

namespace Pegadas.Modules.Audit;

/// <summary>
/// Append-only audit log of changes and sensitive reads (schema <c>audit</c>). The app's
/// database role can only INSERT and SELECT here. Implemented with the Organization module (Phase 3).
/// </summary>
public sealed class AuditModule : IModule
{
    public const string Schema = "audit";

    public string Name => "Audit";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
