namespace Pegadas.BuildingBlocks.Persistence;

/// <summary>Names of the EF Core named query filters applied by <see cref="ModuleDbContext"/>.</summary>
public static class QueryFilters
{
    /// <summary><c>school_id = current tenant</c>. Never ignore this outside jobs that set the tenant explicitly.</summary>
    public const string Tenant = "Tenant";

    /// <summary><c>deleted_at IS NULL</c>. Ignore it for sync pulls (tombstones) and audits.</summary>
    public const string SoftDelete = "SoftDelete";
}
