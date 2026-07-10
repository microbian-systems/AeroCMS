using Aero.Cms.Modules.Footer.Rendering;
using Aero.Cms.Modules.Footer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Aero.Cms.Modules.Footer.ViewComponents;

/// <summary>
/// Represents a class for AeroFooterViewComponent.
/// </summary>
public sealed class AeroFooterViewComponent(
    ISiteContext siteContext,
    IFooterService footerService,
    FooterContext footerContext,
    IFooterHtmlRenderer footerRenderer,
    ILogger<AeroFooterViewComponent> logger) : ViewComponent
{
        /// <summary>
    /// InvokeAsync method.
    /// </summary>
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
