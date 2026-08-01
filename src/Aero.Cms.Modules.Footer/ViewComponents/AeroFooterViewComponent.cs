using Aero.Cms.Modules.Footer.Rendering;
using Aero.Cms.Modules.Footer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Aero.Cms.Modules.Footer.ViewComponents;

/// <summary>
/// Resolves and renders the published footer for the current site.
/// </summary>
/// <remarks>
/// Resolution uses the request-aborted token. A resolution failure is logged and rendering continues
/// with the snapshot currently held by the scoped <see cref="FooterContext"/>.
/// </remarks>
public sealed class AeroFooterViewComponent(
    ISiteContext siteContext,
    IFooterService footerService,
    FooterContext footerContext,
    IFooterHtmlRenderer footerRenderer,
    ILogger<AeroFooterViewComponent> logger) : ViewComponent
{
    /// <summary>
    /// Resolves the current site's culture-appropriate footer and returns its HTML content.
    /// </summary>
    /// <returns>A view-component result containing the rendered footer, or empty content when no snapshot is available.</returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = await footerContext.ResolveAsync(siteContext.SiteId, footerService, HttpContext.RequestAborted);
        if (result is Result<bool, AeroError>.Failure failure)
        {
            logger.LogWarning("Failed to resolve footer: {Error}", failure.Error);
        }

        return new HtmlContentViewComponentResult(footerRenderer.Render(footerContext.Snapshot));
    }
}
