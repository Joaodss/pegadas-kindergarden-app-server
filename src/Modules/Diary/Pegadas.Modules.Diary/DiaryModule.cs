using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegadas.BuildingBlocks.Modules;

namespace Pegadas.Modules.Diary;

/// <summary>
/// The core: diary entries (individual and group), naps, room days and closing the day, and
/// the offline sync endpoints (schema <c>diary</c>). Implemented in Phase 5.
/// </summary>
public sealed class DiaryModule : IModule
{
    public const string Schema = "diary";

    public string Name => "Diary";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
