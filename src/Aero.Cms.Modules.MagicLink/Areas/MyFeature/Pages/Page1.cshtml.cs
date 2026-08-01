using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.MagicLink.Areas.MyFeature.Pages
{
        /// <summary>
    /// Provides the code-behind model for the module's <c>Page1</c> Razor Page.
    /// </summary>
    /// <remarks>
    /// The associated page renders an empty body. This model implements no
    /// magic-link flow: it does not generate, store, hash, expire, deliver,
    /// validate, or consume tokens, and it does not select a user or tenant or
    /// process a return URL. It declares no authorization or rate-limiting
    /// metadata; host-level Razor Pages conventions and policies can still
    /// affect access.
    /// </remarks>
public class Page1Model : PageModel
    {
                /// <summary>
        /// Handles a GET request without changing model state, processing a token,
        /// or issuing a redirect.
        /// </summary>
public void OnGet()
        {

        }
    }
}
