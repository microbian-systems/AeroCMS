using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Fail-closed fallback used when the Content module is not installed.
/// </summary>
internal sealed class UnavailableContentCompositionResolver : IContentCompositionResolver
{
    public Task<Result<PublishedContentItemProjection, AeroError>> ResolveItemAsync(
        long siteId,
        string culture,
        PageContentItemScope scope,
        CancellationToken ct = default)
        => Task.FromResult(Prelude.Fail<PublishedContentItemProjection, AeroError>(Unavailable()));

    public Task<Result<PublishedContentPage, AeroError>> ResolveListAsync(
        long siteId,
        string culture,
        PageContentListScope scope,
        int pageNumber,
        CancellationToken ct = default)
        => Task.FromResult(Prelude.Fail<PublishedContentPage, AeroError>(Unavailable()));

    private static AeroError Unavailable()
        => AeroError.ConfigurationError(
            "Typed page content cannot be rendered because the Content module resolver is unavailable.");
}
