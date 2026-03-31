using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Core;
using Marten;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Docs.Areas.Docs.Pages;

public class DocModel(IQuerySession session) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    public DocsPage? MarkdownPage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        // Ensure we search with the "docs/" prefix if Slug is set
        var pageSlug = string.IsNullOrWhiteSpace(Slug) ? "docs" : "docs/" + Slug.TrimStart('/');
        
        MarkdownPage = await session.Query<DocsPage>()
            .FirstOrDefaultAsync(p => p.Slug == pageSlug && p.PublicationState == ContentPublicationState.Published, cancellationToken);

        if (MarkdownPage is null)
        {
            return NotFound();
        }

        return Page();
    }
}
