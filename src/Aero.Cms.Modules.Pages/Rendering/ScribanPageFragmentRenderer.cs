using System.Collections.Immutable;
using System.Text.Json;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using Scriban.Runtime;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Executes PageEditor Scriban with the existing bounded secure runtime, an
/// explicit page/site context, output sanitization, and final HTML import.
/// </summary>
public sealed class ScribanPageFragmentRenderer(
    ISecureScribanRenderer scribanRenderer,
    IHtmlFragmentImporter htmlImporter) : IPageFragmentRenderer
{
    private readonly ISecureScribanRenderer _scribanRenderer = scribanRenderer
        ?? throw new ArgumentNullException(nameof(scribanRenderer));
    private readonly IHtmlFragmentImporter _htmlImporter = htmlImporter
        ?? throw new ArgumentNullException(nameof(htmlImporter));

    /// <inheritdoc />
    public PageRenderedFragmentKind Kind => PageRenderedFragmentKind.Scriban;

    /// <inheritdoc />
    public async Task<Result<HtmlPageContent>> RenderAsync(
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(context);

        var fields = JsonSerializer.SerializeToElement(
            new Dictionary<string, object?>(),
            JsonSerializerOptions.Default);
        var model = new ScribanContentRenderModel(
            fields,
            new ScribanContentItemRenderScope(
                0,
                context.Slug ?? string.Empty,
                context.Title,
                context.Culture,
                "PageFragment",
                1,
                string.Empty,
                null,
                null,
                fields),
            new ScribanContentTypeRenderScope(
                0,
                "page-fragment",
                "Page fragment",
                null,
                null,
                ImmutableArray<ScribanContentFieldRenderScope>.Empty),
            new ScribanSiteRenderScope(
                context.SiteId,
                context.Culture,
                null,
                null,
                null));
        var page = new ScriptObject
        {
            ["id"] = context.PageId,
            ["title"] = context.Title,
            ["slug"] = context.Slug,
            ["path"] = context.Path,
            ["culture"] = context.Culture
        };
        foreach (var name in new[] { "id", "title", "slug", "path", "culture" })
        {
            page.SetReadOnly(name, readOnly: true);
        }

        var definition = new ScribanRenderDefinition(
            fragment.NodeId,
            1,
            ContentTypeTemplateGenerator.NormalizeTemplate(fragment.Source, []),
            DataSchema: null);
        var output = await _scribanRenderer.RenderAsync(
            definition,
            model,
            cancellationToken,
            new Dictionary<string, ScriptObject>(StringComparer.Ordinal)
            {
                ["page"] = page,
                ["content"] = ContentQueryToScribanMapper.CreateContentScope(
                    context.ContentQueries)
            });
        if (output is Result<string, AeroError>.Failure failure)
        {
            return new Result<HtmlPageContent>.Failure(failure.Error);
        }

        return _htmlImporter.Import(((Result<string, AeroError>.Ok)output).Value);
    }
}
