using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

[OutputCache(PolicyName = "BlogPolicy")]
public class BlogDetailPageModel(IBlogPostContentService blogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    public BlogPostDocument? Post { get; private set; }
    public Dictionary<long, string> TagNames { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Slug))
        {
            return NotFound();
        }

        // Slugs are stored without a prefix — the route /blog/{slug}
        // provides the plain slug directly from the URL.
        var result = await blogService.FindBySlugAsync(Slug, cancellationToken);
        var tagsResult = await blogService.GetAllTagsAsync(cancellationToken);

        var post = result switch
        {
            Result<BlogPostDocument?, AeroError>.Ok(var foundPost) => foundPost,
            _ => (BlogPostDocument?)null
        };

        if (post is null)
        {
            return NotFound();
        }

        TagNames = tagsResult switch
        {
            Result<IReadOnlyList<Tag>, AeroError>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };

        Post = post;
        return Page();
    }
}
