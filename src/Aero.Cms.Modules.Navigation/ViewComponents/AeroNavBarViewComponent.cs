using Aero.Cms.Modules.Navigation.Rendering;
using Aero.Cms.Modules.Navigation.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Modules.Navigation.ViewComponents;

/// <summary>
/// Resolves the request's published navigation snapshot and renders the navigation view.
/// </summary>
/// <remarks>
/// Resolution failures are logged and the view still runs with the scoped
/// <see cref="NavMenuContext"/>, allowing the view to render an empty state.
/// </remarks>
public sealed class AeroNavBarViewComponent(
    ISiteContext siteContext,
    INavMenuService navMenuService,
    NavMenuContext navMenuContext,
    ILogger<AeroNavBarViewComponent> logger) : ViewComponent
{
    /// <summary>
    /// Resolves a page override or the current site's default culture-aware navigation menu.
    /// </summary>
    /// <param name="pageOverrideId">An optional menu identifier supplied by trusted page configuration.</param>
    /// <returns>The default component view with the resolved <see cref="NavMenuContext"/> as its model.</returns>
    /// <remarks>
    /// The site-scoped resolver validates the override against the current site. Invalid,
    /// unpublished, archived, or foreign overrides render with no navigation snapshot.
    /// </remarks>
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
