using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Applies Aero's strict HTML import, site style, and static rendering pipeline to
/// markup produced by a source strategy.
/// </summary>
public sealed class PageMarkupRenderer(
    IHtmlFragmentImporter htmlImporter,
    HtmlStaticRenderer htmlRenderer,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver)
{
    /// <summary>Validates and renders one source-produced HTML fragment.</summary>
    public async Task<Result<RenderedPage>> RenderAsync(
        long siteId,
        string markup,
        IReadOnlyList<string> contentTypeAliases,
        CancellationToken cancellationToken = default)
    {
        var imported = htmlImporter.Import(markup);
        if (imported is Result<HtmlPageContent>.Failure importFailure)
        {
            return importFailure.Error;
        }

        var profileResult = await styleProfileResolver.ResolveAsync(siteId, cancellationToken);
        if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
        {
            return profileFailure.Error;
        }

        var content = ((Result<HtmlPageContent>.Ok)imported).Value;
        var profile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
        var compiled = styleCompiler.Compile(content, profile);
        if (compiled is Result<CompiledPageStyles>.Failure styleFailure)
        {
            return styleFailure.Error;
        }

        var rendered = htmlRenderer.RenderPage(
            content,
            ((Result<CompiledPageStyles>.Ok)compiled).Value);
        if (rendered is Result<RenderedHtmlPage>.Failure renderFailure)
        {
            return renderFailure.Error;
        }

        var page = ((Result<RenderedHtmlPage>.Ok)rendered).Value;
        return new RenderedPage(
            page.Markup,
            page.CssText,
            contentTypeAliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }
}
