using Aero.Cms.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages;

public class DynamicPageModel(IPageContentService pageService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    public PageDocument? PageDocument { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        Result<string, PageDocument?> result;

        // If no slug provided, load the homepage
        if (string.IsNullOrWhiteSpace(Slug))
        {
            result = await pageService.LoadHomepageAsync(cancellationToken);
        }
        else
        {
            // Normalize slug - remove leading slash if present for consistency
            var normalizedSlug = Slug.TrimStart('/');
            result = await pageService.FindBySlugAsync(normalizedSlug, cancellationToken);
        }

        var page = result switch
        {
            global::Aero.Core.Railway.Result<string, PageDocument?>.Ok(var foundPage) => foundPage,
            global::Aero.Core.Railway.Result<string, PageDocument?>.Failure => (PageDocument?)null,
            _ => (PageDocument?)null
        };

        if (page is null)
        {
            return NotFound();
        }

        PageDocument = page;
        return Page();
    }
}
