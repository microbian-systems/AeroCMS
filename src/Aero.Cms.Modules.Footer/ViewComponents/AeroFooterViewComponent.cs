using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Footer.Rendering;
using Aero.Cms.Modules.Footer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Modules.Footer.ViewComponents;

public sealed class AeroFooterViewComponent(
    ISiteContext siteContext,
    IFooterService footerService,
    FooterContext footerContext,
    ILogger<AeroFooterViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = await footerContext.ResolveAsync(siteContext.SiteId, footerService, HttpContext.RequestAborted);
        if (result is Result<bool, AeroError>.Failure failure)
        {
            logger.LogWarning("Failed to resolve footer: {Error}", failure.Error);
        }

        return View(footerContext);
    }
}
