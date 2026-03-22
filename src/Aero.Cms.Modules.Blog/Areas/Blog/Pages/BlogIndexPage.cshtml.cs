using Aero.Cms.Modules.Blog.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

public class BlogIndexPageModel : PageModel
{
    private readonly IBlogPostContentService _blogService;

    public BlogIndexPageModel(IBlogPostContentService blogService)
    {
        _blogService = blogService;
    }

    public IReadOnlyList<BlogPostDocument> Posts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> FeaturedPosts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> OtherPosts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _blogService.GetLatestPostsAsync(10, cancellationToken);

        Posts = result switch
        {
            global::Aero.Core.Railway.Result<string, IReadOnlyList<BlogPostDocument>>.Ok(var posts) => posts,
            global::Aero.Core.Railway.Result<string, IReadOnlyList<BlogPostDocument>>.Failure => [],
            _ => []
        };

        // Split posts into featured (top 3) and other (remaining)
        if (Posts.Any())
        {
            FeaturedPosts = Posts.Take(3).ToList();
            OtherPosts = Posts.Skip(3).ToList();
        }
    }
}