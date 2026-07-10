using System.Linq.Expressions;
using Aero.Cms.Abstractions.Enums;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Docs.Queries;

/// <summary>
/// All published docs for a site, ordered by <see cref="DocsPage.Order"/>.
/// </summary>
public sealed class DocsPublishedBySiteIdQuery : ICompiledQuery<DocsPage, IEnumerable<DocsPage>>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<DocsPage>, IEnumerable<DocsPage>>> QueryIs()
        => q => q
            .Where(x => x.SiteId == SiteId
                     && x.PublicationState == ContentPublicationState.Published)
            .OrderBy(x => x.Order);
}

/// <summary>
/// Paged subset of published docs for a site.
/// </summary>
public sealed class DocsPublishedBySiteIdPagedQuery : ICompiledQuery<DocsPage, IEnumerable<DocsPage>>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Skip.
    /// </summary>
public int Skip { get; set; }
        /// <summary>
    /// Gets or sets the Take.
    /// </summary>
public int Take { get; set; } = 10;

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<DocsPage>, IEnumerable<DocsPage>>> QueryIs()
        => q => q
            .Where(x => x.SiteId == SiteId
                     && x.PublicationState == ContentPublicationState.Published)
            .OrderBy(x => x.Order)
            .Skip(Skip)
            .Take(Take);
}

/// <summary>
/// Total count of published docs for a site.
/// </summary>
public sealed class DocsPublishedCountBySiteIdQuery : ICompiledQuery<DocsPage, long>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<DocsPage>, long>> QueryIs()
        => q => q
            .Where(x => x.SiteId == SiteId
                     && x.PublicationState == ContentPublicationState.Published)
            .Count();
}

/// <summary>
/// Single published doc by site and slug.
/// </summary>
public sealed class DocsPublishedBySlugQuery : ICompiledQuery<DocsPage, DocsPage?>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public required string Slug { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<DocsPage>, DocsPage?>> QueryIs()
        => q => q.FirstOrDefault(x =>
            x.SiteId == SiteId
            && x.Slug == Slug
            && x.PublicationState == ContentPublicationState.Published);
}
