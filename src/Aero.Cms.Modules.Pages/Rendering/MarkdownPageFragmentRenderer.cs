using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Renders Markdown with raw HTML disabled and imports the result through the
/// shared HTML catalog, attribute, URL, and content-model boundary.
/// </summary>
public sealed class MarkdownPageFragmentRenderer(IMarkdownInterchangeAdapter markdown)
    : IPageFragmentRenderer
{
    private readonly IMarkdownInterchangeAdapter _markdown = markdown
        ?? throw new ArgumentNullException(nameof(markdown));

    /// <inheritdoc />
    public PageRenderedFragmentKind Kind => PageRenderedFragmentKind.Markdown;

    /// <inheritdoc />
    public Task<Result<HtmlPageContent>> RenderAsync(
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_markdown.Import(fragment.Source ?? string.Empty));
    }
}
