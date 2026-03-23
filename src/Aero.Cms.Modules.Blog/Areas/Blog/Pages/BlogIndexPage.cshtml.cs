using Aero.Cms.Modules.Blog.Models;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

public class BlogIndexPageModel(IBlogPostContentService blogService) : PageModel
{
    public IReadOnlyList<BlogPostDocument> Posts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> FeaturedPosts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> OtherPosts { get; private set; } = [];
    public Dictionary<long, string> TagNames { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await blogService.GetLatestPostsAsync(10, cancellationToken);
        var tagsResult = await blogService.GetAllTagsAsync(cancellationToken);

        Posts = result switch
        {
            Result<string, IReadOnlyList<BlogPostDocument>>.Ok(var posts) => posts,
            _ => []
        };

        TagNames = tagsResult switch
        {
            Result<string, IReadOnlyList<Tag>>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };

        // Split posts into featured (top 3) and other (remaining)
        if (Posts.Any())
        {
            FeaturedPosts = Posts.Take(3).ToList();
            OtherPosts = Posts.Skip(3).Take(10).ToList();
        }
    }
}