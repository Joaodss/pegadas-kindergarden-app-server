using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.BuildingBlocks.Modules;

namespace Pegadas.Modules.Identity;

/// <summary>
/// Devices, staff credentials, PIN sign-in, device refresh tokens and access-token issuance
/// (schema <c>identity</c>). Implemented in Phase 4.
/// </summary>
public sealed class IdentityModule : IModule
{
    public const string Schema = "identity";

    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
