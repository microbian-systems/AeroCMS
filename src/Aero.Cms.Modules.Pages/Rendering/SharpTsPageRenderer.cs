using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Renders a pure TypeScript page through the interpret-only SharpTS host.</summary>
public sealed class SharpTsPageRenderer(
    ISharpTsExecutor executor,
    PageMarkupRenderer markupRenderer) : IPageRenderer
{
    public PageRendererId Id { get; } = new(PageRendererIds.SharpTs);

    public PageRendererDescriptor Descriptor { get; } = new(
        PageRendererIds.SharpTs,
        "TypeScript",
        PageEditorKinds.Source,
        SupportsFragments: true,
        IsExperimental: true,
        SourceLanguage: "typescript",
        InitialSource: """
            export function render(context: any) {
                return html`
                    <main class="aero-page">
                        <h1>${context.page.title}</h1>
                    </main>`;
            }
            """);

    public async Task<Result<RenderedPage>> RenderAsync(
        PageRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceResult = SourcePageRendererValidation.Validate(
            request,
            PageRendererIds.SharpTs,
            "TypeScript");
        if (sourceResult is Result<PageRenderSource>.Failure sourceFailure)
        {
            return sourceFailure.Error;
        }

        var rendered = await executor.ExecuteAsync(
            ((Result<PageRenderSource>.Ok)sourceResult).Value.Source,
            SharpTsRenderContext.Create(
                request.Metadata,
                request.ContentQueries,
                request.IsPreview),
            maximumOutputLength: 200_000,
            cancellationToken);
        if (rendered is Result<string>.Failure renderFailure)
        {
            return renderFailure.Error;
        }

        return await markupRenderer.RenderAsync(
            request.Metadata.SiteId,
            ((Result<string>.Ok)rendered).Value,
            request.ContentQueries.ContentTypeAliases,
            cancellationToken);
    }
}
