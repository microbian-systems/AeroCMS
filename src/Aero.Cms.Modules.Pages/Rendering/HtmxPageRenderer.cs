using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Renders a pure HTMX page through Aero's validated HTML pipeline.</summary>
public sealed class HtmxPageRenderer(PageMarkupRenderer markupRenderer) : IPageRenderer
{
    public PageRendererId Id { get; } = new(PageRendererIds.Htmx);

    public PageRendererDescriptor Descriptor { get; } = new(
        PageRendererIds.Htmx,
        "HTMX",
        PageEditorKinds.Source,
        SupportsFragments: true,
        IsExperimental: true,
        SourceLanguage: "html",
        InitialSource: """
            <main class="aero-page">
              <h1>New HTMX page</h1>
              <button type="button" hx-get="/api/example" hx-target="#result">Load content</button>
              <section id="result" aria-live="polite"></section>
            </main>
            """);

    public async Task<Result<RenderedPage>> RenderAsync(
        PageRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceResult = SourcePageRendererValidation.Validate(
            request,
            PageRendererIds.Htmx,
            "HTMX");
        if (sourceResult is Result<PageRenderSource>.Failure sourceFailure)
        {
            return sourceFailure.Error;
        }

        return await markupRenderer.RenderAsync(
            request.Metadata.SiteId,
            ((Result<PageRenderSource>.Ok)sourceResult).Value.Source,
            request.ContentQueries.ContentTypeAliases,
            cancellationToken);
    }
}
