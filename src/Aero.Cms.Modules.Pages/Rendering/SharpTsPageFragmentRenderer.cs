using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Renders a TypeScript fragment through SharpTS and Aero's strict HTML importer.</summary>
public sealed class SharpTsPageFragmentRenderer(
    ISharpTsExecutor executor,
    IHtmlFragmentImporter htmlImporter) : IPageFragmentRenderer
{
    public PageRenderedFragmentKind Kind => PageRenderedFragmentKind.SharpTs;

    public async Task<Result<HtmlPageContent>> RenderAsync(
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        var metadata = new PageRenderMetadata(
            context.PageId > 0 ? context.PageId : null,
            context.SiteId,
            PageRendererIds.SharpTs,
            context.Title ?? "Preview",
            context.Slug ?? "preview",
            context.Path ?? "/preview",
            context.Culture);
        var rendered = await executor.ExecuteAsync(
            fragment.Source,
            SharpTsRenderContext.Create(
                metadata,
                context.ContentQueries,
                context.IsPreview),
            maximumOutputLength: 100_000,
            cancellationToken);
        if (rendered is Result<string>.Failure renderFailure)
        {
            return renderFailure.Error;
        }

        return htmlImporter.Import(((Result<string>.Ok)rendered).Value);
    }
}
