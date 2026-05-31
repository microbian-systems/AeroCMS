using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Shared.Components;
using Aero.Cms.Shared.Localization;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Http.Extensions;
using System.Globalization;

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
    public string RequestedCulture { get; private set; } = "en-US";
    public string RenderedCulture { get; private set; } = "en-US";
    public bool IsCultureFallback { get; private set; }
    public string CanonicalUrl { get; private set; } = string.Empty;
    public IReadOnlyList<AlternatePostLink> AlternateLinks { get; private set; } = [];
    public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        PostViewModel? post;
        RequestedCulture = CultureInfo.CurrentUICulture.Name;

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
            post = await postActor.FindBySlugAsync(
                AeroCultureRoute.StripLeadingCulture(Slug),
                siteContext.SiteId,
                RequestedCulture,
                cancellationToken);
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
        RenderedCulture = post.Culture;
        IsCultureFallback = !string.Equals(RequestedCulture, RenderedCulture, StringComparison.OrdinalIgnoreCase);
        CanonicalUrl = BuildCultureUrl(RenderedCulture, $"blog/{post.Slug}");
        AlternateLinks = await BuildAlternateLinksAsync(post, cancellationToken);
        CultureSwitcherLinks = BuildCultureSwitcherLinks(AlternateLinks);
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RenderedCulture;
        ViewData["IsCultureFallback"] = IsCultureFallback;
        ViewData["CultureSwitcherLinks"] = CultureSwitcherLinks;
        ApplyResponseCacheHeaders();
        return Page();
    }

    private async Task<IReadOnlyList<AlternatePostLink>> BuildAlternateLinksAsync(PostViewModel post, CancellationToken cancellationToken)
    {
        var variants = await postActor.ListCultureVariantsAsync(post.Id, cancellationToken);
        if (variants.Count == 0)
            variants = [post];

        var publishedVariants = variants
            .Where(variant => variant.PublicationState == ContentPublicationState.Published)
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Culture) && !string.IsNullOrWhiteSpace(variant.Slug))
            .GroupBy(variant => variant.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (publishedVariants.Count == 0 && post.PublicationState == ContentPublicationState.Published)
            publishedVariants.Add(post);

        var links = publishedVariants
            .Select(variant => new AlternatePostLink(
                variant.Culture.ToLowerInvariant(),
                BuildCultureUrl(variant.Culture, $"blog/{variant.Slug}")))
            .ToList();

        var defaultCulture = HttpContext.Features.Get<IAeroSiteSlice>()?.DefaultCulture ?? post.Culture;
        var defaultVariant = publishedVariants.FirstOrDefault(variant =>
            string.Equals(variant.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase));

        if (defaultVariant is not null)
            links.Add(new AlternatePostLink("x-default", BuildCultureUrl(defaultVariant.Culture, $"blog/{defaultVariant.Slug}")));

        return links;
    }

    private IReadOnlyList<CultureSwitcherLink> BuildCultureSwitcherLinks(IReadOnlyList<AlternatePostLink> alternateLinks)
        => alternateLinks
            .Where(link => !string.Equals(link.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase))
            .Select(link => CultureSwitcher.CreateLink(
                link.Hreflang,
                link.Href,
                string.Equals(link.Hreflang, RequestedCulture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(link.Hreflang, RenderedCulture, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(link => link.Hreflang, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

    private string BuildCultureUrl(string culture, string? slug)
        => UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(culture, slug));

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

    public sealed record AlternatePostLink(string Hreflang, string Href);
}
