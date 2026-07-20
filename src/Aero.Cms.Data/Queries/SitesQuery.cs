using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;

/// <summary>Selects the first site-host document whose stored host exactly matches a supplied value.</summary>
/// <remarks>The expression performs no hostname normalization and returns <see langword="null"/> when no host matches.</remarks>
public sealed class SiteByHostnameQuery : ICompiledQuery<SiteHost, SiteHost?>
{
    /// <summary>The pre-normalized hostname used by the equality predicate.</summary>
public string hostname { get; set; } = null!;

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<SiteHost>, SiteHost?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => x.Host == hostname);
    }
}

/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class SiteByIdQuery : EntityByIdQuery<SitesModel>;

/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class SitesByIdsQuery : EntitiesByIdsQuery<SitesModel>;

/// <summary>Selects sites owned by one tenant, ordered by stored site name.</summary>
public sealed class SitesByTenantIdQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    /// <summary>The tenant identifier that must match <see cref="SitesModel.TenantId"/>.</summary>
public required long TenantId { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToList();
    }
}


/// <summary>Selects sites whose stored name exactly matches a supplied value.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored site name.</remarks>
public sealed class SitesByNameQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    /// <summary>The site name used by the equality predicate.</summary>
public required string Name { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects enabled sites, ordered by stored site name.</summary>
public sealed class EnabledSitesQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects disabled sites, ordered by stored site name.</summary>
public sealed class DisabledSitesQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => !x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects sites whose stored default culture exactly matches a supplied value.</summary>
/// <remarks>The expression performs no culture normalization and orders matches by stored site name.</remarks>
public sealed class SitesByDefaultCultureQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    /// <summary>The culture value used by the equality predicate.</summary>
public required string DefaultCulture { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.DefaultCulture == DefaultCulture)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class SitesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<SitesModel>;

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class SitesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<SitesModel>;
