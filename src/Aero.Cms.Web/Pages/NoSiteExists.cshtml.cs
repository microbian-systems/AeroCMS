using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Web.Pages;

public class NoSiteExistsModel : PageModel
{
    public string RequestedHost { get; set; } = "";

    public void OnGet()
    {
        RequestedHost = HttpContext.Request.Host.Host;
    }
}
