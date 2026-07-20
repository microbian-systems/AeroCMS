using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Mail.Areas.MyFeature.Pages
{
        /// <summary>
    /// Provides the code-behind model for the module's <c>Page1</c> Razor Page.
    /// </summary>
    /// <remarks>
    /// The associated page renders an empty body and exposes no mail composition,
    /// queue, provider, delivery, or status interface. This model declares no
    /// authorization metadata; effective access can still be governed by
    /// host-level Razor Pages conventions and authorization policies.
    /// </remarks>
public class Page1Model : PageModel
    {
                /// <summary>
        /// Handles a GET request without changing model state or performing mail work.
        /// </summary>
public void OnGet()
        {

        }
    }
}
