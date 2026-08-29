using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Preserves Aero's visual composition pipeline behind the full-page renderer strategy.
/// </summary>
public sealed class AeroCompositionPageRenderer(
    PageCompositionExpander compositionExpander,
    HtmlStaticRenderer htmlRenderer,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver) : IPageRenderer
{
    /// <inheritdoc />
    public PageRendererId Id { get; } = new(PageRendererIds.AeroComposition);

    /// <inheritdoc />
    public PageRendererDescriptor Descriptor { get; } = new(
        PageRendererIds.AeroComposition,
        "Aero",
        PageEditorKinds.VisualComposition,
        SupportsFragments: true,
        IsExperimental: false);

    /// <inheritdoc />
    public async Task<Result<RenderedPage>> RenderAsync(
        PageRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var expansionResult = await compositionExpander.ExpandAsync(
            request.Metadata.SiteId,
            request.Metadata.Culture,
            request.Content,
            request.Composition,
            request.ContentPageNumbers,
            cancellationToken,
            new PageFragmentRenderContext
            {
                SiteId = request.Metadata.SiteId,
                Culture = request.Metadata.Culture,
                PageId = request.Metadata.Id ?? 0,
                Title = request.Metadata.Title,
                Slug = request.Metadata.Slug,
                Path = request.Metadata.Path,
                ContentQueries = request.ContentQueries,
                IsPreview = request.IsPreview
            },
            request.RouteValues);
        if (expansionResult is Result<PageCompositionExpansion, Aero.Core.AeroError>.Failure expansionFailure)
        {
            return expansionFailure.Error;
        }

        var expansion =
            ((Result<PageCompositionExpansion, Aero.Core.AeroError>.Ok)expansionResult).Value;
        var profileResult = await styleProfileResolver.ResolveAsync(
            request.Metadata.SiteId,
            cancellationToken);
        if (profileResult is Result<IStyleProfile, Aero.Core.AeroError>.Failure profileFailure)
        {
            return profileFailure.Error;
        }

        var styleProfile = ((Result<IStyleProfile, Aero.Core.AeroError>.Ok)profileResult).Value;
        var compiled = styleCompiler.Compile(expansion.Content, styleProfile);
        if (compiled is Result<CompiledPageStyles>.Failure styleFailure)
        {
            return styleFailure.Error;
        }

        var rendered = htmlRenderer.RenderPage(
            expansion.Content,
            ((Result<CompiledPageStyles>.Ok)compiled).Value);
        if (rendered is Result<RenderedHtmlPage>.Failure renderFailure)
        {
            return renderFailure.Error;
        }

        var page = ((Result<RenderedHtmlPage>.Ok)rendered).Value;
        var aliases = expansion.ContentTypeAliases
            .Concat(request.ContentQueries.ContentTypeAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new RenderedPage(
            page.Markup,
            page.CssText,
            aliases);
    }
}
