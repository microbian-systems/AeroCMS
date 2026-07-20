using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Web.Pages;

/// <summary>
/// Backs the page shown when host-based site resolution finds no site.
/// </summary>
public class NoSiteExistsModel : PageModel
{
    /// <summary>
    /// Gets or sets the hostname that failed site resolution.
    /// </summary>
public string RequestedHost { get; set; } = "";

    /// <summary>
    /// Captures the current request hostname for display.
    /// </summary>
public void OnGet()
    {
        RequestedHost = HttpContext.Request.Host.Host;
    }
}
