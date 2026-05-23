using Aero.Cms.Modules.Navigation.Rendering;
using Aero.Cms.Modules.Navigation.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Modules.Navigation.ViewComponents;

public sealed class AeroNavBarViewComponent(
    ISiteContext siteContext,
    INavMenuService navMenuService,
    NavMenuContext navMenuContext,
    ILogger<AeroNavBarViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(long? pageOverrideId = null)
    {
        var result = await navMenuContext.ResolveAsync(
            siteContext.SiteId,
            pageOverrideId,
            navMenuService,
            HttpContext.RequestAborted);

        if (result is Result<bool, AeroError>.Failure failure)
        {
            logger.LogWarning("Failed to resolve navigation menu: {Error}", failure.Error);
        }

        return View(navMenuContext);
    }
}
