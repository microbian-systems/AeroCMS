using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <summary>
/// Represents a class for PageByIdQuery.
/// </summary>
public sealed class PageByIdQuery : EntityByIdQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesByIdsQuery.
/// </summary>
public sealed class PagesByIdsQuery : EntitiesByIdsQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesCreatedByQuery.
/// </summary>
public sealed class PagesCreatedByQuery : EntitiesByCreatedByQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesModifiedByQuery.
/// </summary>
public sealed class PagesModifiedByQuery : EntitiesByModifiedByQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesCreatedOnRangeQuery.
/// </summary>
public sealed class PagesCreatedOnRangeQuery : EntitiesCreatedInRangeQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesModifiedOnRangeQuery.
/// </summary>
public sealed class PagesModifiedOnRangeQuery : EntitiesModifiedInRangeQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesByCreatedByInDateRangeQuery.
/// </summary>
public sealed class PagesByCreatedByInDateRangeQuery : EntitiesByCreatedByInDateRangeQuery<PageDocument>;
/// <summary>
/// Represents a class for PagesByModifiedByInDateRangeQuery.
/// </summary>
public sealed class PagesByModifiedByInDateRangeQuery : EntitiesByModifiedByInDateRangeQuery<PageDocument>;
/// <summary>
/// Represents a class for LatestPageCreatedByQuery.
/// </summary>
public sealed class LatestPageCreatedByQuery : LatestCreatedByQuery<PageDocument>;
/// <summary>
/// Represents a class for LatestPageModifiedByQuery.
/// </summary>
public sealed class LatestPageModifiedByQuery : LatestModifiedByQuery<PageDocument>;

/// <summary>
/// Compiled query: finds all descendant pages by materialized path prefix.
/// Used by PageTreeService.MoveAsync and NavigationService.MarkHiddenDescendantsAsync
/// to avoid re-compiling the LINQ expression tree on each call.
///
/// Marten's LINQ provider translates <c>Path.StartsWith(prefix)</c> to a
/// PostgreSQL prefix match (leveraging the NgramIndex on Path).
/// </summary>
public sealed class PagesByPathPrefixQuery : ICompiledQuery<PageDocument, IList<PageDocument>>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Path Prefix.
    /// </summary>
public required string PathPrefix { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<IMartenQueryable<PageDocument>, IList<PageDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.Path.StartsWith(PathPrefix))
            .ToList();
    }
}