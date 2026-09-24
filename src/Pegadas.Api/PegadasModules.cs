using Pegadas.BuildingBlocks.Modules;
using Pegadas.Modules.Audit;
using Pegadas.Modules.Diary;
using Pegadas.Modules.Identity;
using Pegadas.Modules.Notifications;
using Pegadas.Modules.Organization;
using Pegadas.Modules.Summaries;

namespace Pegadas.Api;

/// <summary>Every module composed into the host. Registered explicitly: no assembly scanning at startup.</summary>
internal static class PegadasModules
{
    public static readonly IReadOnlyList<IModule> All =
    [
        new IdentityModule(),
        new OrganizationModule(),
        new DiaryModule(),
        new SummariesModule(),
        new AuditModule(),
        new NotificationsModule(),
    ];
}
