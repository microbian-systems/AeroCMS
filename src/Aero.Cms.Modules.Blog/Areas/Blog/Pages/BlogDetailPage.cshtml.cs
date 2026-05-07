using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "BlogPolicy")]
public class BlogDetailPageModel(IBlogPostContentService blogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    public BlogPostDocument? Post { get; private set; }
    public Dictionary<long, string> TagNames { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        Result<BlogPostDocument?, AeroError>? result;

        if (DraftId is { } draftId)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            result = await blogService.LoadAsync(draftId, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            return NotFound();
        }
        else
        {
            // Slugs are stored without a prefix — the route /blog/{slug}
            // provides the plain slug directly from the URL.
            result = await blogService.FindBySlugAsync(Slug, cancellationToken);
        }

        var post = result switch
        {
            Result<BlogPostDocument?, AeroError>.Ok(var foundPost) => foundPost,
            _ => (BlogPostDocument?)null
        };

        if (post is null)
        {
            return NotFound();
        }

        TagNames = (await blogService.GetAllTagsAsync(cancellationToken)) switch
        {
            Result<IReadOnlyList<Tag>, AeroError>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };

        Post = post;
        ApplyResponseCacheHeaders();
        return Page();
    }

    private void ApplyResponseCacheHeaders()
    {
        if (DraftId is not null)
        {
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            return;
        }

        Response.Headers.CacheControl = "public,max-age=300";
    }
}
