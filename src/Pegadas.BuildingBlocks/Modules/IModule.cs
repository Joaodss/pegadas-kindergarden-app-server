using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Pegadas.BuildingBlocks.Modules;

/// <summary>
/// Entry point of a module. It is the only public type in a module's implementation
/// assembly (enforced by the architecture tests); everything else is <c>internal</c>.
/// </summary>
public interface IModule
{
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's endpoints onto the versioned route group (<c>/v1</c>).</summary>
    void MapEndpoints(IEndpointRouteBuilder v1);
}
