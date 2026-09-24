namespace Pegadas.BuildingBlocks.Security;

/// <summary>Claim names in Pegadas access tokens. Inbound claim mapping is disabled, so these are the raw JWT names.</summary>
public static class PegadasClaimTypes
{
    /// <summary>Staff member id.</summary>
    public const string Subject = "sub";

    /// <summary>Tenant. The only source of the current school for a request.</summary>
    public const string SchoolId = "school_id";

    public const string DeviceId = "device_id";

    /// <summary>Room ids the staff member works in (one claim per room).</summary>
    public const string Rooms = "rooms";

    public const string Role = "role";
}

public static class PegadasRoles
{
    public const string Educator = "educator";

    public const string Coordinator = "coordinator";
}

public static class PegadasPolicies
{
    /// <summary>Educators and coordinators of the token's school.</summary>
    public const string Educator = "educator";

    /// <summary>Coordinators of the token's school.</summary>
    public const string Coordinator = "coordinator";
}
