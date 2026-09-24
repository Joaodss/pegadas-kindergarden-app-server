using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegadas.BuildingBlocks.Modules;
using Pegadas.Modules.Notifications.Contracts;

namespace Pegadas.Modules.Notifications;

/// <summary>
/// Notification sender and channels. The MVP has no push or email (shared room tablets, no
/// parents yet), so every channel is a no-op behind its interface; real providers replace the
/// registrations here without touching callers.
/// </summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<INotificationSender, NoOpNotificationSender>();
        services.TryAddSingleton<IPushChannel, NoOpPushChannel>();
        services.TryAddSingleton<IEmailChannel, NoOpEmailChannel>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
