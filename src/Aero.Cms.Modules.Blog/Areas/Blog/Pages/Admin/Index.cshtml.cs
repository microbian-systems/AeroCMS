using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly IBlogPostContentService _blogPostContentService;

    public IndexModel(IBlogPostContentService blogPostContentService)
    {
        _blogPostContentService = blogPostContentService;
    }

    public IReadOnlyList<BlogPostDocument> Posts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _blogPostContentService.GetLatestPostsAsync(1000, cancellationToken);

        Posts = result switch
        {
            Result<string, IReadOnlyList<BlogPostDocument>>.Ok(var posts) => posts,
            Result<string, IReadOnlyList<BlogPostDocument>>.Failure => [],
            _ => []
        };
    }
}