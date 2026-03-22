using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Pages.Areas.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly IPageContentService _pageContentService;

    public IndexModel(IPageContentService pageContentService)
    {
        _pageContentService = pageContentService;
    }

    public IReadOnlyList<PageDocument> Pages { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pageContentService.GetAllPagesAsync(cancellationToken);

        Pages = result switch
        {
            Result<string, IReadOnlyList<PageDocument>>.Ok(var pages) => pages,
            Result<string, IReadOnlyList<PageDocument>>.Failure => [],
            _ => []
        };
    }
}
