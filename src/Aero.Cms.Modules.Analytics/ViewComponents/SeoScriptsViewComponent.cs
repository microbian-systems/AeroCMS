using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Aero.Cms.Modules.Analytics.ViewComponents;

/// <summary>
/// Renders analytics markup for a named document placement in a Razor view.
/// </summary>
/// <param name="renderer">The scoped renderer used for the current request.</param>
public sealed class SeoScriptsViewComponent(ISeoScriptRenderer renderer) : ViewComponent
{
    /// <summary>
    /// Parses a placement name and returns its rendered analytics markup.
    /// </summary>
    /// <param name="placement">A case-insensitive <see cref="SeoScriptPlacement"/> name.</param>
    /// <returns>Empty content for an unrecognized placement; otherwise, the renderer output.</returns>
public async Task<IViewComponentResult> InvokeAsync(string placement)
    {
        if (!Enum.TryParse<SeoScriptPlacement>(placement, ignoreCase: true, out var parsed))
        {
            return new HtmlContentViewComponentResult(HtmlString.Empty);
        }

        var content = await renderer.RenderAsync(parsed, HttpContext.RequestAborted);
        return new HtmlContentViewComponentResult(content);
    }
}
