using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Web.Pages;

/// <summary>
/// Represents a class for NoSiteExistsModel.
/// </summary>
public class NoSiteExistsModel : PageModel
{
        /// <summary>
    /// Gets or sets the Requested Host.
    /// </summary>
public string RequestedHost { get; set; } = "";

        /// <summary>
    /// OnGet method.
    /// </summary>
public void OnGet()
    {
        RequestedHost = HttpContext.Request.Host.Host;
    }
}
