using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Content.Composition;

/// <summary>
/// Resolves published, site-scoped content projections for page composition.
/// </summary>
/// <remarks>
/// The Content module owns the implementation. Consumers receive immutable projections
/// rather than persistence entities, keeping content storage and query behavior outside Pages.
/// </remarks>
public interface IContentCompositionResolver
{
    /// <summary>Resolves one published content item for a page scope.</summary>
    Task<Result<PublishedContentItemProjection, AeroError>> ResolveItemAsync(
        long siteId,
        string culture,
        PageContentItemScope scope,
        CancellationToken ct = default);

    /// <summary>Resolves one page of published content items for a list scope.</summary>
    Task<Result<PublishedContentPage, AeroError>> ResolveListAsync(
        long siteId,
        string culture,
        PageContentListScope scope,
        int pageNumber,
        CancellationToken ct = default);
}

