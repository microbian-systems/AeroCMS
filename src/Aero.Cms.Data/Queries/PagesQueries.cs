using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class PageByIdQuery : EntityByIdQuery<PageDocument>;
/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class PagesByIdsQuery : EntitiesByIdsQuery<PageDocument>;
/// <inheritdoc cref="EntitiesByCreatedByQuery{T}"/>
public sealed class PagesCreatedByQuery : EntitiesByCreatedByQuery<PageDocument>;
/// <inheritdoc cref="EntitiesByModifiedByQuery{T}"/>
public sealed class PagesModifiedByQuery : EntitiesByModifiedByQuery<PageDocument>;
/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class PagesCreatedOnRangeQuery : EntitiesCreatedInRangeQuery<PageDocument>;
/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class PagesModifiedOnRangeQuery : EntitiesModifiedInRangeQuery<PageDocument>;
/// <inheritdoc cref="EntitiesByCreatedByInDateRangeQuery{T}"/>
public sealed class PagesByCreatedByInDateRangeQuery : EntitiesByCreatedByInDateRangeQuery<PageDocument>;
/// <inheritdoc cref="EntitiesByModifiedByInDateRangeQuery{T}"/>
public sealed class PagesByModifiedByInDateRangeQuery : EntitiesByModifiedByInDateRangeQuery<PageDocument>;
/// <inheritdoc cref="LatestCreatedByQuery{T}"/>
public sealed class LatestPageCreatedByQuery : LatestCreatedByQuery<PageDocument>;
/// <inheritdoc cref="LatestModifiedByQuery{T}"/>
public sealed class LatestPageModifiedByQuery : LatestModifiedByQuery<PageDocument>;

/// <summary>
/// Selects pages in one site whose materialized path starts with a supplied prefix.
/// </summary>
/// <remarks>
/// The expression passes <see cref="PathPrefix"/> directly to
/// <see cref="string.StartsWith(string)"/> without normalization and declares no
/// result ordering. Whether the page at the prefix itself is included depends on
/// whether its stored path starts with the exact supplied value.
/// </remarks>
public sealed class PagesByPathPrefixQuery : ICompiledQuery<PageDocument, IList<PageDocument>>
{
    /// <summary>The site identifier that must match <see cref="PageDocument.SiteId"/>.</summary>
public required long SiteId { get; set; }
    /// <summary>The unmodified path prefix used by the starts-with predicate.</summary>
public required string PathPrefix { get; set; }

    /// <inheritdoc />
public Expression<Func<ISableQueryable<PageDocument>, IList<PageDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.Path.StartsWith(PathPrefix))
            .ToList();
    }
}
