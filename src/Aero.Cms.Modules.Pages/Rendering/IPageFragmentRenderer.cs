using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Expands one source-backed composition fragment into validated HTML nodes.
/// </summary>
public interface IPageFragmentRenderer
{
    /// <summary>Gets the fragment strategy handled by this renderer.</summary>
    PageRenderedFragmentKind Kind { get; }

    /// <summary>Renders one bounded fragment into an independent HTML tree.</summary>
    Task<Result<HtmlPageContent>> RenderAsync(
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default);
}
