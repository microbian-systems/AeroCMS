using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Pages.Areas.Admin.Pages;

public class PagesIndexModel(IPageContentService pageContentService) : PageModel
{
    public IReadOnlyList<PageDocument> Pages { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await pageContentService.GetAllPagesAsync(cancellationToken);

        Pages = result switch
        {
            Result<string, IReadOnlyList<PageDocument>>.Ok(var pages) => pages,
            Result<string, IReadOnlyList<PageDocument>>.Failure => [],
            _ => []
        };
    }
}
