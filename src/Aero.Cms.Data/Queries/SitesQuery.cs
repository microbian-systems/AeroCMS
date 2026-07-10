using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;

/// <summary>
/// Represents a class for SiteByHostnameQuery.
/// </summary>
public sealed class SiteByHostnameQuery : ICompiledQuery<SiteHost, SiteHost?>
{
        /// <summary>
    /// Gets or sets the hostname.
    /// </summary>
public string hostname { get; set; } = null!;

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<SiteHost>, SiteHost?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => x.Host == hostname);
    }
}

/// <summary>
/// Represents a class for SiteByIdQuery.
/// </summary>
public sealed class SiteByIdQuery : EntityByIdQuery<SitesModel>;

/// <summary>
/// Represents a class for SitesByIdsQuery.
/// </summary>
public sealed class SitesByIdsQuery : EntitiesByIdsQuery<SitesModel>;

/// <summary>
/// Represents a class for SitesByTenantIdQuery.
/// </summary>
public sealed class SitesByTenantIdQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
public required long TenantId { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToList();
    }
}


/// <summary>
/// Represents a class for SitesByNameQuery.
/// </summary>
public sealed class SitesByNameQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for EnabledSitesQuery.
/// </summary>
public sealed class EnabledSitesQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for DisabledSitesQuery.
/// </summary>
public sealed class DisabledSitesQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => !x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for SitesByDefaultCultureQuery.
/// </summary>
public sealed class SitesByDefaultCultureQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
public required string DefaultCulture { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.DefaultCulture == DefaultCulture)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for SitesCreatedInRangeQuery.
/// </summary>
public sealed class SitesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<SitesModel>;

/// <summary>
/// Represents a class for SitesModifiedInRangeQuery.
/// </summary>
public sealed class SitesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<SitesModel>;