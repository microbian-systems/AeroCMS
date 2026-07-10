using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <summary>
/// Represents a class for TenantByIdQuery.
/// </summary>
public sealed class TenantByIdQuery : EntityByIdQuery<TenantModel>;

/// <summary>
/// Represents a class for TenantsByIdsQuery.
/// </summary>
public sealed class TenantsByIdsQuery : EntitiesByIdsQuery<TenantModel>;

/// <summary>
/// Represents a class for TenantByNameQuery.
/// </summary>
public sealed class TenantByNameQuery : ICompiledQuery<TenantModel, TenantModel?>
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<IMartenQueryable<TenantModel>, TenantModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Name == Name);
    }
}

/// <summary>
/// Represents a class for TenantByHostnameQuery.
/// </summary>
public sealed class TenantByHostnameQuery : ICompiledQuery<TenantModel, TenantModel?>
{
        /// <summary>
    /// Gets or sets the Hostname.
    /// </summary>
public required string Hostname { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<IMartenQueryable<TenantModel>, TenantModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Hostname == Hostname);
    }
}

/// <summary>
/// Represents a class for TenantsByNotesQuery.
/// </summary>
public sealed class TenantsByNotesQuery : ICompiledQuery<TenantModel, IList<TenantModel>>
{
        /// <summary>
    /// Gets or sets the Notes.
    /// </summary>
public required string Notes { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<IMartenQueryable<TenantModel>, IList<TenantModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Notes == Notes)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for TenantsCreatedInRangeQuery.
/// </summary>
public sealed class TenantsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<TenantModel>;

/// <summary>
/// Represents a class for TenantsModifiedInRangeQuery.
/// </summary>
public sealed class TenantsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<TenantModel>;