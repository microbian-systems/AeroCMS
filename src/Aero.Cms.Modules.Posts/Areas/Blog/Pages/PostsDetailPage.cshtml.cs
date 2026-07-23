using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Shared.Components;
using Aero.Cms.Shared.Localization;
using Aero.Core.Http;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Http.Extensions;
using System.Globalization;

namespace Aero.Cms.Modules.Posts.Areas.Blog.Pages;

/// <summary>
/// Loads a public culture-aware post page or an authenticated draft preview.
/// </summary>
/// <param name="postActor">The actor used for post, taxonomy, and author queries.</param>
/// <param name="siteContext">The current site boundary.</param>
/// <remarks>
/// Public lookups return only published posts. Draft lookups require an authenticated principal,
/// are constrained to the current site by the actor, and disable response caching.
/// </remarks>
[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "BlogPolicy")]
public class PostsDetailPageModel(
    IAeroPostActor postActor,
    ISiteContext siteContext) : PageModel
{
    private static readonly MarkdownPipeline BlogMarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers()
        .Build();

    /// <summary>
    /// Gets or sets the route slug for a public post request.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the draft identifier supplied by the internal preview route.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    /// <summary>
    /// Gets the post selected for rendering after a successful request.
    /// </summary>
public PostViewModel? Post { get; private set; }
    /// <summary>
    /// Gets the current site's mapping of tag identifiers to display names.
    /// </summary>
public Dictionary<long, string> TagNames { get; private set; } = [];
    /// <summary>
    /// Gets the optional author summary for the selected post.
    /// </summary>
public (string? Name, string? Bio, string? AvatarUrl)? PostAuthor { get; private set; }
    /// <summary>
    /// Gets the request UI culture used for lookup.
    /// </summary>
public string RequestedCulture { get; private set; } = "en-US";
    /// <summary>
    /// Gets the culture of the variant that was actually rendered.
    /// </summary>
public string RenderedCulture { get; private set; } = "en-US";
    /// <summary>
    /// Gets a value indicating whether the rendered variant differs from the requested culture.
    /// </summary>
public bool IsCultureFallback { get; private set; }
    /// <summary>
    /// Gets the absolute culture-specific URL for the rendered variant.
    /// </summary>
public string CanonicalUrl { get; private set; } = string.Empty;
    /// <summary>
    /// Gets the absolute alternate URLs for published culture variants.
    /// </summary>
public IReadOnlyList<AlternatePostLink> AlternateLinks { get; private set; } = [];
    /// <summary>
    /// Gets the alternate URLs projected for the site culture switcher.
    /// </summary>
public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];
    /// <summary>
    /// Gets the server-rendered Markdown HTML with stable heading anchors.
    /// </summary>
public string RenderedMarkdown { get; private set; } = string.Empty;
    /// <summary>
    /// Gets the second- and third-level article headings for the on-page navigation.
    /// </summary>
public IReadOnlyList<BlogTableOfContentsItem> TableOfContents { get; private set; } = [];

    /// <summary>
    /// Resolves the requested post and prepares canonical, alternate, taxonomy, and author data.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel actor calls.</param>
    /// <returns>
    /// The page result, <c>401</c> for an unauthenticated draft request, or <c>404</c> when no
    /// eligible post can be resolved.
    /// </returns>
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
        RenderedMarkdown = string.IsNullOrWhiteSpace(post.MarkdownContent)
            ? string.Empty
            : Markdown.ToHtml(post.MarkdownContent, BlogMarkdownPipeline);
        TableOfContents = ExtractTableOfContents(post.MarkdownContent);
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

    private static IReadOnlyList<BlogTableOfContentsItem> ExtractTableOfContents(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var document = Markdown.Parse(markdown, BlogMarkdownPipeline);
        var headings = new List<BlogTableOfContentsItem>();

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            if (heading.Level is not (2 or 3))
                continue;

            var anchorId = heading.TryGetAttributes()?.Id;
            var text = heading.Inline?.FirstChild?.ToString();
            if (string.IsNullOrWhiteSpace(anchorId) || string.IsNullOrWhiteSpace(text))
                continue;

            headings.Add(new BlogTableOfContentsItem(text, anchorId, heading.Level));
        }

        return headings;
    }

    /// <summary>
    /// Builds alternate links from distinct published variants in the post's translation group.
    /// </summary>
    /// <param name="post">The rendered post used as a fallback when the actor returns no variants.</param>
    /// <param name="cancellationToken">A token used to cancel the actor call.</param>
    /// <returns>Culture links plus an <c>x-default</c> link when the default-culture variant exists.</returns>
    private async Task<IReadOnlyList<AlternatePostLink>> BuildAlternateLinksAsync(PostViewModel post, CancellationToken cancellationToken)
    {
        var variants = await postActor.ListCultureVariantsAsync(
            post.Id,
            siteContext.SiteId,
            cancellationToken);
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

    /// <summary>
    /// Converts alternate links into unique culture-switcher entries and marks the active culture.
    /// </summary>
    /// <param name="alternateLinks">The SEO alternate links, including any <c>x-default</c> entry.</param>
    /// <returns>Unique culture links excluding <c>x-default</c>.</returns>
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

    /// <summary>
    /// Builds an absolute URL under the request scheme, host, and path base.
    /// </summary>
    /// <param name="culture">The culture segment to normalize into the path.</param>
    /// <param name="slug">The optional path following the culture segment.</param>
    /// <returns>The absolute culture-specific URL.</returns>
    private string BuildCultureUrl(string culture, string? slug)
        => UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(culture, slug));

    /// <summary>
    /// Disables storage for draft previews or enables a five-minute public cache for published pages.
    /// </summary>
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

    /// <summary>
    /// Associates an SEO language code with an absolute post URL.
    /// </summary>
    /// <param name="Hreflang">The normalized culture code or <c>x-default</c>.</param>
    /// <param name="Href">The absolute alternate URL.</param>
public sealed record AlternatePostLink(string Hreflang, string Href);

/// <summary>
/// Describes an anchored article heading shown in the public post table of contents.
/// </summary>
public sealed record BlogTableOfContentsItem(string Text, string AnchorId, int Level);
}
