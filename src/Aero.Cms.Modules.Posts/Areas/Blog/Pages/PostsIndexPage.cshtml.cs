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

/// <summary>
/// Builds the culture-aware public blog index and its paged partial response.
/// </summary>
/// <param name="postActor">The actor used for post and taxonomy queries.</param>
/// <param name="siteContext">The current site boundary.</param>
/// <remarks>
/// The first three published posts are featured. Remaining results are paged after skipping those
/// entries so the same post is not rendered in both sections.
/// </remarks>
[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByQueryKeys = ["p"])]
[OutputCache(PolicyName = "BlogPolicy")]
public class PostsIndexPageModel(
    IAeroPostActor postActor,
    ISiteContext siteContext) : PageModel
{
    /// <summary>
    /// Gets the requested page number.
    /// </summary>
public int PageNumber { get; private set; } = 1;
    /// <summary>
    /// Gets the number of non-featured posts requested per page.
    /// </summary>
public int PageSize { get; private set; } = 10;
    /// <summary>
    /// Gets the total number of non-featured posts reported by the actor.
    /// </summary>
public int TotalCount { get; private set; }
    /// <summary>
    /// Gets the total page count for non-featured posts.
    /// </summary>
public int TotalPages { get; private set; }
    /// <summary>
    /// Gets a value indicating whether a later page is available.
    /// </summary>
public bool HasNextPage { get; private set; }
    /// <summary>
    /// Gets a value indicating whether an earlier page is available.
    /// </summary>
public bool HasPreviousPage { get; private set; }

    /// <summary>
    /// Gets up to three latest published posts for the requested culture.
    /// </summary>
public List<PostViewModel> FeaturedPosts { get; private set; } = [];
    /// <summary>
    /// Gets the current page of published posts after the featured entries.
    /// </summary>
public List<PostViewModel> OtherPosts { get; private set; } = [];
    /// <summary>
    /// Gets the current site's mapping of tag identifiers to display names.
    /// </summary>
public Dictionary<long, string> TagNames { get; private set; } = [];
    /// <summary>
    /// Gets the request UI culture used for post queries and route generation.
    /// </summary>
public string RequestedCulture { get; private set; } = "en-US";
    /// <summary>
    /// Gets the absolute URL for the requested-culture blog index.
    /// </summary>
public string CanonicalUrl { get; private set; } = string.Empty;
    /// <summary>
    /// Gets alternate index URLs for each supported site culture and <c>x-default</c>.
    /// </summary>
public IReadOnlyList<AlternateBlogIndexLink> AlternateLinks { get; private set; } = [];

    /// <summary>
    /// Loads featured posts, a page of remaining posts, taxonomy names, and alternate links.
    /// </summary>
    /// <param name="p">The optional page number; omitted values default to one.</param>
    /// <param name="cancellationToken">A token used to cancel actor calls.</param>
    /// <returns>A task that completes when page state has been populated.</returns>
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

    /// <summary>
    /// Loads one page of non-featured posts for the Razor Page partial handler.
    /// </summary>
    /// <param name="p">The requested page number.</param>
    /// <param name="cancellationToken">A token used to cancel actor calls.</param>
    /// <returns>The <c>_PostsList</c> partial populated with this page model.</returns>
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

    /// <summary>
    /// Builds the culture-aware public URL for a post.
    /// </summary>
    /// <param name="post">The post whose slug is included in the route.</param>
    /// <returns>A path that follows the current request's culture-prefix style.</returns>
public string BuildPostUrl(PostViewModel post)
        => AeroCultureRoute.BuildCulturePathForCurrentRequest(HttpContext, RequestedCulture, $"blog/{post.Slug}");

    /// <summary>
    /// Builds the partial-handler URL for another blog index page.
    /// </summary>
    /// <param name="pageNumber">The page number placed in the query string.</param>
    /// <returns>A culture-aware blog path with the handler and page query parameters.</returns>
public string BuildPostsPageUrl(int pageNumber)
    {
        var path = AeroCultureRoute.BuildCulturePathForCurrentRequest(HttpContext, RequestedCulture, "blog");
        return $"{path}?handler=PostsPage&p={pageNumber}";
    }

    /// <summary>
    /// Loads a page after skipping the three entries reserved for the featured section.
    /// </summary>
    /// <param name="pageNumber">The page number passed to the actor.</param>
    /// <param name="cancellationToken">A token used to cancel the actor call.</param>
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

    /// <summary>
    /// Builds an absolute URL under the request scheme, host, and path base.
    /// </summary>
    /// <param name="culture">The culture segment to normalize into the path.</param>
    /// <param name="slug">The optional path following the culture segment.</param>
    /// <returns>The absolute culture-specific URL.</returns>
    private string BuildCultureUrl(string culture, string? slug)
        => UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(culture, slug));

    /// <summary>
    /// Builds one blog index link per normalized supported culture plus the default link.
    /// </summary>
    /// <returns>The alternate URL collection; it always includes an <c>x-default</c> entry.</returns>
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

    /// <summary>
    /// Associates an SEO language code with an absolute blog index URL.
    /// </summary>
    /// <param name="Hreflang">The normalized culture code or <c>x-default</c>.</param>
    /// <param name="Href">The absolute alternate URL.</param>
public sealed record AlternateBlogIndexLink(string Hreflang, string Href);
}
