using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using System.Globalization;

namespace Aero.Cms.Modules.Posts.Areas.Blog.Pages;

[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByQueryKeys = ["p"])]
[OutputCache(PolicyName = "BlogPolicy")]
public class PostsIndexPageModel(
    IAeroPostActor postActor,
    ISiteContext siteContext) : PageModel
{
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 10;
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool HasNextPage { get; private set; }
    public bool HasPreviousPage { get; private set; }

    public List<PostViewModel> FeaturedPosts { get; private set; } = [];
    public List<PostViewModel> OtherPosts { get; private set; } = [];
    public Dictionary<long, string> TagNames { get; private set; } = [];

    public async Task OnGetAsync(int? p, CancellationToken cancellationToken = default)
    {
        PageNumber = p ?? 1;

        var (featured, _) = await postActor.GetLatestPostsAsync(siteContext.SiteId, 3, CultureInfo.CurrentUICulture.Name, cancellationToken);
        FeaturedPosts = featured;

        await LoadOtherPostsAsync(PageNumber, cancellationToken);

        TagNames = await postActor.GetTagNameMapAsync(siteContext.SiteId, cancellationToken);
    }

    public async Task<IActionResult> OnGetPostsPageAsync(int p, CancellationToken cancellationToken = default)
    {
        PageNumber = p;
        await LoadOtherPostsAsync(PageNumber, cancellationToken);

        TagNames = await postActor.GetTagNameMapAsync(siteContext.SiteId, cancellationToken);

        return Partial("_PostsList", this);
    }

    private async Task LoadOtherPostsAsync(int pageNumber, CancellationToken cancellationToken)
    {
        var (items, totalCount, totalPages, hasNext, hasPrev) = await postActor.GetPagedPostsAsync(
            siteContext.SiteId,
            pageNumber,
            PageSize,
            skipFromLatest: 3,
            culture: CultureInfo.CurrentUICulture.Name,
            ct: cancellationToken);

        OtherPosts = items;
        TotalCount = totalCount;
        TotalPages = totalPages;
        HasNextPage = hasNext;
        HasPreviousPage = hasPrev;
    }
}
