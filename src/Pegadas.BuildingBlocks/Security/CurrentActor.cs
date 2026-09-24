using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Pegadas.BuildingBlocks.Security;

/// <summary>Who is making the current request: staff member, device and room scope, read from the access token.</summary>
public interface ICurrentActor
{
    bool IsAuthenticated { get; }

    Guid? StaffId { get; }

    Guid? DeviceId { get; }

    bool IsCoordinator { get; }

    IReadOnlySet<Guid> RoomIds { get; }

    /// <summary>
    /// Room-scope check used against BOLA/IDOR. Coordinators can access every room of their
    /// school; the school boundary itself is enforced by the tenant query filter.
    /// </summary>
    bool CanAccessRoom(Guid roomId);
}

internal sealed class CurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    private IReadOnlySet<Guid>? _roomIds;

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? StaffId => ReadGuid(PegadasClaimTypes.Subject);

    public Guid? DeviceId => ReadGuid(PegadasClaimTypes.DeviceId);

    public bool IsCoordinator => User?.IsInRole(PegadasRoles.Coordinator) == true;

    public IReadOnlySet<Guid> RoomIds => _roomIds ??= ReadRooms();

    public bool CanAccessRoom(Guid roomId) => IsAuthenticated && (IsCoordinator || RoomIds.Contains(roomId));

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(User?.FindFirst(claimType)?.Value, out var id) ? id : null;

    private HashSet<Guid> ReadRooms()
    {
        var rooms = new HashSet<Guid>();
        if (User is { } user)
        {
            foreach (var claim in user.FindAll(PegadasClaimTypes.Rooms))
            {
                if (Guid.TryParse(claim.Value, out var id))
                {
                    rooms.Add(id);
                }
            }
        }

        return rooms;
    }
}
