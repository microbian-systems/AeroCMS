using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Shared.Localization;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Http.Extensions;
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
    public string RequestedCulture { get; private set; } = "en-US";
    public string CanonicalUrl { get; private set; } = string.Empty;
    public IReadOnlyList<AlternateBlogIndexLink> AlternateLinks { get; private set; } = [];

    public async Task OnGetAsync(int? p, CancellationToken cancellationToken = default)
    {
        PageNumber = p ?? 1;
        RequestedCulture = CultureInfo.CurrentUICulture.Name;
        CanonicalUrl = BuildCultureUrl(RequestedCulture, "blog");
        AlternateLinks = BuildAlternateLinks();
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RequestedCulture;

        var (featured, _) = await postActor.GetLatestPostsAsync(siteContext.SiteId, 3, RequestedCulture, cancellationToken);
        FeaturedPosts = featured;

        await LoadOtherPostsAsync(PageNumber, cancellationToken);

        TagNames = await postActor.GetTagNameMapAsync(siteContext.SiteId, cancellationToken);
    }

    public async Task<IActionResult> OnGetPostsPageAsync(int p, CancellationToken cancellationToken = default)
    {
        PageNumber = p;
        RequestedCulture = CultureInfo.CurrentUICulture.Name;
        CanonicalUrl = BuildCultureUrl(RequestedCulture, "blog");
        AlternateLinks = BuildAlternateLinks();
        await LoadOtherPostsAsync(PageNumber, cancellationToken);

        TagNames = await postActor.GetTagNameMapAsync(siteContext.SiteId, cancellationToken);

        return Partial("_PostsList", this);
    }

    public string BuildPostUrl(PostViewModel post)
        => AeroCultureRoute.BuildCulturePathForCurrentRequest(HttpContext, RequestedCulture, $"blog/{post.Slug}");

    public string BuildPostsPageUrl(int pageNumber)
    {
        var path = AeroCultureRoute.BuildCulturePathForCurrentRequest(HttpContext, RequestedCulture, "blog");
        return $"{path}?handler=PostsPage&p={pageNumber}";
    }

    private async Task LoadOtherPostsAsync(int pageNumber, CancellationToken cancellationToken)
    {
        var (items, totalCount, totalPages, hasNext, hasPrev) = await postActor.GetPagedPostsAsync(
            siteContext.SiteId,
            pageNumber,
            PageSize,
            skipFromLatest: 3,
            culture: RequestedCulture,
            ct: cancellationToken);

        OtherPosts = items;
        TotalCount = totalCount;
        TotalPages = totalPages;
        HasNextPage = hasNext;
        HasPreviousPage = hasPrev;
    }

    private string BuildCultureUrl(string culture, string? slug)
        => UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(culture, slug));

    private IReadOnlyList<AlternateBlogIndexLink> BuildAlternateLinks()
    {
        var site = HttpContext.Features.Get<IAeroSiteSlice>();
        var defaultCulture = AeroCultureRoute.NormalizeCultureOrDefault(site?.DefaultCulture, RequestedCulture);
        var supportedCultures = AeroCultureRoute.NormalizeSupportedCultures(site?.SupportedCultures, defaultCulture);

        var links = supportedCultures
            .Select(culture => new AlternateBlogIndexLink(culture.ToLowerInvariant(), BuildCultureUrl(culture, "blog")))
            .ToList();

        links.Add(new AlternateBlogIndexLink("x-default", BuildCultureUrl(defaultCulture, "blog")));
        return links;
    }

    public sealed record AlternateBlogIndexLink(string Hreflang, string Href);
}
