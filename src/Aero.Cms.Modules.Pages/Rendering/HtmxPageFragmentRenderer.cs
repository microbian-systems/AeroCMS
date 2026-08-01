using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Imports a validated HTMX island into an Aero composition.</summary>
public sealed class HtmxPageFragmentRenderer(
    IHtmlFragmentImporter htmlImporter) : IPageFragmentRenderer
{
    public PageRenderedFragmentKind Kind => PageRenderedFragmentKind.Htmx;

    public Task<Result<HtmlPageContent>> RenderAsync(
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(htmlImporter.Import(fragment.Source));
    }
}
