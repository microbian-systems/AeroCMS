using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Posts.Areas.Blog.Pages;

[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "BlogPolicy")]
public class PostsDetailPageModel(
    IAeroPostActor postActor,
    ISiteContext siteContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    public PostViewModel? Post { get; private set; }
    public Dictionary<long, string> TagNames { get; private set; } = [];
    public (string? Name, string? Bio, string? AvatarUrl)? PostAuthor { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        PostViewModel? post;

        if (DraftId is { } draftId)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            post = await postActor.LoadAsync(draftId, siteContext.SiteId, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            return NotFound();
        }
        else
        {
            post = await postActor.FindBySlugAsync(Slug, siteContext.SiteId, cancellationToken);
        }

        if (post is null)
        {
            return NotFound();
        }

        TagNames = await postActor.GetTagNameMapAsync(siteContext.SiteId, cancellationToken);

        if (post.AuthorId is { } authorId)
        {
            PostAuthor = await postActor.GetPostAuthorSummaryAsync(siteContext.SiteId, authorId, cancellationToken);
        }

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
