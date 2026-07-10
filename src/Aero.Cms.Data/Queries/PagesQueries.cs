using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


public sealed class PageByIdQuery : EntityByIdQuery<PageDocument>;
public sealed class PagesByIdsQuery : EntitiesByIdsQuery<PageDocument>;
public sealed class PagesCreatedByQuery : EntitiesByCreatedByQuery<PageDocument>;
public sealed class PagesModifiedByQuery : EntitiesByModifiedByQuery<PageDocument>;
public sealed class PagesCreatedOnRangeQuery : EntitiesCreatedInRangeQuery<PageDocument>;
public sealed class PagesModifiedOnRangeQuery : EntitiesModifiedInRangeQuery<PageDocument>;
public sealed class PagesByCreatedByInDateRangeQuery : EntitiesByCreatedByInDateRangeQuery<PageDocument>;
public sealed class PagesByModifiedByInDateRangeQuery : EntitiesByModifiedByInDateRangeQuery<PageDocument>;
public sealed class LatestPageCreatedByQuery : LatestCreatedByQuery<PageDocument>;
public sealed class LatestPageModifiedByQuery : LatestModifiedByQuery<PageDocument>;

/// <summary>
/// Compiled query: finds all descendant pages by materialized path prefix.
/// Used by PageTreeService.MoveAsync and NavigationService.MarkHiddenDescendantsAsync
/// to avoid re-compiling the LINQ expression tree on each call.
///
/// AeroDB's LINQ provider translates <c>Path.StartsWith(prefix)</c> to a
/// PostgreSQL prefix match (leveraging the NgramIndex on Path).
/// </summary>
public sealed class PagesByPathPrefixQuery : ICompiledQuery<PageDocument, IList<PageDocument>>
{
    public required long SiteId { get; set; }
    public required string PathPrefix { get; set; }

    public Expression<Func<ISurrealDbQueryable<PageDocument>, IList<PageDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.Path.StartsWith(PathPrefix))
            .ToList();
    }
}