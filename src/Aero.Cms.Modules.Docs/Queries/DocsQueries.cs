using System.Linq.Expressions;
using Aero.Cms.Abstractions.Enums;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Docs.Queries;

/// <summary>
/// Defines a compiled query for all published documentation pages in a site.
/// </summary>
public sealed class DocsPublishedBySiteIdQuery : ICompiledQuery<DocsPage, IEnumerable<DocsPage>>
{
    /// <summary>
    /// Gets or sets the site identifier used by the query predicate.
    /// </summary>
public required long SiteId { get; set; }

    /// <summary>
    /// Builds the provider expression that filters published pages and orders them by display order.
    /// </summary>
    /// <returns>The query expression executed by the document provider.</returns>
public Expression<Func<ISableQueryable<DocsPage>, IEnumerable<DocsPage>>> QueryIs()
        => q => q
            .Where(x => x.SiteId == SiteId
                     && x.PublicationState == ContentPublicationState.Published)
            .OrderBy(x => x.Order);
}

/// <summary>
/// Defines a compiled query for a positional page of published documentation in a site.
/// </summary>
public sealed class DocsPublishedBySiteIdPagedQuery : ICompiledQuery<DocsPage, IEnumerable<DocsPage>>
{
    /// <summary>
    /// Gets or sets the site identifier used by the query predicate.
    /// </summary>
public required long SiteId { get; set; }

    /// <summary>
    /// Gets or sets the number of ordered records to omit.
    /// </summary>
public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of records to return.
    /// </summary>
public int Take { get; set; } = 10;

    /// <summary>
    /// Builds the provider expression that filters, orders, skips, and takes records.
    /// </summary>
    /// <returns>The query expression executed by the document provider.</returns>
public Expression<Func<ISableQueryable<DocsPage>, IEnumerable<DocsPage>>> QueryIs()
        => q => q
            .Where(x => x.SiteId == SiteId
                     && x.PublicationState == ContentPublicationState.Published)
            .OrderBy(x => x.Order)
            .Skip(Skip)
            .Take(Take);
}

/// <summary>
/// Defines a compiled query that counts published documentation pages in a site.
/// </summary>
public sealed class DocsPublishedCountBySiteIdQuery : ICompiledQuery<DocsPage, long>
{
    /// <summary>
    /// Gets or sets the site identifier used by the query predicate.
    /// </summary>
public required long SiteId { get; set; }

    /// <summary>
    /// Builds the provider count expression.
    /// </summary>
    /// <returns>The query expression executed by the document provider.</returns>
public Expression<Func<ISableQueryable<DocsPage>, long>> QueryIs()
        => q => q
            .Where(x => x.SiteId == SiteId
                     && x.PublicationState == ContentPublicationState.Published)
            .Count();
}

/// <summary>
/// Defines a compiled query for the first published page matching a site and exact slug.
/// </summary>
public sealed class DocsPublishedBySlugQuery : ICompiledQuery<DocsPage, DocsPage?>
{
    /// <summary>
    /// Gets or sets the site identifier used by the query predicate.
    /// </summary>
public required long SiteId { get; set; }

    /// <summary>
    /// Gets or sets the exact stored slug to match.
    /// </summary>
public required string Slug { get; set; }

    /// <summary>
    /// Builds the provider expression that returns the first matching published page.
    /// </summary>
    /// <returns>The query expression executed by the document provider.</returns>
    /// <remarks>The query does not filter by culture.</remarks>
public Expression<Func<ISableQueryable<DocsPage>, DocsPage?>> QueryIs()
        => q => q.FirstOrDefault(x =>
            x.SiteId == SiteId
            && x.Slug == Slug
            && x.PublicationState == ContentPublicationState.Published);
}
