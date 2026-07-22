using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Imports Custom HTML through the shared catalog, attribute, URL, nesting,
/// and full-tree validation boundary.
/// </summary>
public sealed class CustomHtmlPageFragmentRenderer(IHtmlFragmentImporter htmlImporter)
    : IPageFragmentRenderer
{
    private readonly IHtmlFragmentImporter _htmlImporter = htmlImporter
        ?? throw new ArgumentNullException(nameof(htmlImporter));

    /// <inheritdoc />
    public PageRenderedFragmentKind Kind => PageRenderedFragmentKind.CustomHtml;

    /// <inheritdoc />
    public Task<Result<HtmlPageContent>> RenderAsync(
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_htmlImporter.Import(fragment.Source ?? string.Empty));
    }
}
