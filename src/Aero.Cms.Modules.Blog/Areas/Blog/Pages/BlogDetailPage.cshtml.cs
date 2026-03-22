using Aero.Cms.Modules.Blog.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

public class BlogDetailPageModel : PageModel
{
    private readonly IBlogPostContentService _blogService;

    public BlogDetailPageModel(IBlogPostContentService blogService)
    {
        _blogService = blogService;
    }

    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    public BlogPostDocument? Post { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Slug))
        {
            return NotFound();
        }

        var result = await _blogService.FindBySlugAsync(Slug, cancellationToken);

        var post = result switch
        {
            global::Aero.Core.Railway.Result<string, BlogPostDocument?>.Ok(var foundPost) => foundPost,
            global::Aero.Core.Railway.Result<string, BlogPostDocument?>.Failure => (BlogPostDocument?)null,
            _ => (BlogPostDocument?)null
        };

        if (post is null)
        {
            return NotFound();
        }

        Post = post;
        return Page();
    }
}