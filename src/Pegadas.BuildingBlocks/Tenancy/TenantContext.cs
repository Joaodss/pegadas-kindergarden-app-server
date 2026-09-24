using Microsoft.AspNetCore.Http;
using Pegadas.BuildingBlocks.Security;

namespace Pegadas.BuildingBlocks.Tenancy;

/// <summary>The school (tenant) of the current scope. <c>null</c> means "no tenant": tenant-filtered queries return nothing.</summary>
public interface ITenantContext
{
    Guid? SchoolId { get; }
}

/// <summary>
/// Sets the tenant explicitly for scopes without an HTTP request (jobs, outbox handlers).
/// Must be called before a module <c>DbContext</c> is resolved from the scope.
/// </summary>
public interface ITenantSetter
{
    void SetTenant(Guid schoolId);
}

/// <summary>Reads the tenant from the <c>school_id</c> claim of the access token, never from the request.</summary>
internal sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext, ITenantSetter
{
    private Guid? _explicitSchoolId;
    private Guid? _claimSchoolId;
    private bool _claimRead;

    public Guid? SchoolId => _explicitSchoolId ?? ReadClaim();

    public void SetTenant(Guid schoolId)
    {
        if (schoolId == Guid.Empty)
        {
            throw new ArgumentException("A tenant id cannot be empty.", nameof(schoolId));
        }

        if (_explicitSchoolId is { } current && current != schoolId)
        {
            throw new InvalidOperationException("The tenant of a scope cannot be changed once set.");
        }

        _explicitSchoolId = schoolId;
    }

    private Guid? ReadClaim()
    {
        if (!_claimRead)
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(PegadasClaimTypes.SchoolId)?.Value;
            _claimSchoolId = Guid.TryParse(value, out var id) ? id : null;
            _claimRead = true;
        }

        return _claimSchoolId;
    }
}
