using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Blog.Areas.Admin.Pages;

public class IndexModel(IBlogPostContentService blogPostContentService) : PageModel
{
    public IReadOnlyList<BlogPostDocument> Posts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await blogPostContentService.GetLatestPostsAsync(1000, cancellationToken);

        Posts = result switch
        {
            Result<string, IReadOnlyList<BlogPostDocument>>.Ok(var posts) => posts,
            Result<string, IReadOnlyList<BlogPostDocument>>.Failure => [],
            _ => []
        };
    }
}