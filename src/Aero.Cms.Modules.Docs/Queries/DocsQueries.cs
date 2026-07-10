using System.Linq.Expressions;
using Aero.Cms.Abstractions.Enums;
using AeroDB;

namespace Aero.Cms.Modules.Docs.Queries;

/// <summary>
/// All published docs for a site, ordered by <see cref="DocsPage.Order"/>.
/// </summary>
public sealed class DocsPublishedBySiteIdQuery : ICompiledQuery<DocsPage, IEnumerable<DocsPage>>
{
    public required long SiteId { get; set; }

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
    public required long SiteId { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 10;

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
    public required long SiteId { get; set; }

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
    public required long SiteId { get; set; }
    public required string Slug { get; set; }

    public Expression<Func<ISurrealDbQueryable<DocsPage>, DocsPage?>> QueryIs()
        => q => q.FirstOrDefault(x =>
            x.SiteId == SiteId
            && x.Slug == Slug
            && x.PublicationState == ContentPublicationState.Published);
}
