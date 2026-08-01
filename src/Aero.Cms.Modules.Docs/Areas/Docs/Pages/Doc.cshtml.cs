using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs.Areas.Docs.Models;
using Aero.Cms.Shared.Components;
using Aero.Cms.Shared.Localization;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Http.Extensions;
using System.Globalization;

namespace Aero.Cms.Modules.Docs.Areas.Docs.Pages;

/// <summary>
/// Loads and prepares a culture-aware public documentation page.
/// </summary>
/// <remarks>
/// Only published content is selected. If the requested culture has no page for the slug,
/// the content service can return the site's default-culture page. Supporting navigation failures
/// are tolerated, while a content lookup failure is rendered as not found. Responses participate
/// in both response caching and the named <c>DocsPolicy</c> output-cache policy.
/// </remarks>
[ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "DocsPolicy")]
public class DocModel : PageModel
{
    private readonly IDocsService _docsService;
    private readonly IDocsTreeService _docsTreeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocModel"/> class.
    /// </summary>
    /// <param name="docsService">The current-site content service.</param>
    /// <param name="docsTreeService">The hierarchy and heading projection service.</param>
public DocModel(IDocsService docsService, IDocsTreeService docsTreeService)
    {
        _docsService = docsService;
        _docsTreeService = docsTreeService;
    }

    /// <summary>
    /// Gets or sets the catch-all path relative to the localized <c>/docs</c> prefix.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets or sets the draft identifier supplied by the authenticated manager preview route.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    /// <summary>
    /// Gets the published page selected for rendering.
    /// </summary>
public DocsPage? MarkdownPage { get; private set; }

    /// <summary>
    /// Gets the selected page's published direct children for overview cards.
    /// </summary>
public IReadOnlyList<DocsPage> ChildPages { get; private set; } = [];

    /// <summary>
    /// Gets the rendered culture's published sidebar hierarchy.
    /// </summary>
public List<DocsSidebarNode> SidebarTree { get; private set; } = [];

    /// <summary>
    /// Gets H2 and H3 entries extracted from the selected page.
    /// </summary>
public List<HeadingItem> OnThisPage { get; private set; } = [];

    /// <summary>
    /// Gets the published breadcrumb chain excluding the virtual docs root.
    /// </summary>
public IReadOnlyList<DocsPage> Breadcrumbs { get; private set; } = [];

    /// <summary>
    /// Gets the normalized UI culture requested by the localized route.
    /// </summary>
public string RequestedCulture { get; private set; } = SitesModel.DefaultCultureName;

    /// <summary>
    /// Gets the stored culture of the page actually rendered.
    /// </summary>
public string RenderedCulture { get; private set; } = SitesModel.DefaultCultureName;

    /// <summary>
    /// Gets whether the rendered page came from a culture other than <see cref="RequestedCulture"/>.
    /// </summary>
public bool IsCultureFallback { get; private set; }

    /// <summary>
    /// Gets the absolute localized URL for the rendered page.
    /// </summary>
public string CanonicalUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Gets published translation links, including <c>x-default</c> when a default variant exists.
    /// </summary>
public IReadOnlyList<AlternateDocLink> AlternateLinks { get; private set; } = [];

    /// <summary>
    /// Gets de-duplicated culture switcher entries derived from published alternate links.
    /// </summary>
public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

    /// <summary>
    /// Resolves the localized document and prepares navigation, SEO links, and cache metadata.
    /// </summary>
    /// <param name="cancellationToken">The token used by content and hierarchy operations.</param>
    /// <returns>The Razor page, or a not-found result when no published page can be resolved.</returns>
    /// <remarks>
    /// The route path is always prefixed with <c>docs/</c> before lookup. Hierarchy and translation
    /// failures do not fail the page; their corresponding collections fall back to empty or current-page data.
    /// </remarks>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        RequestedCulture = CultureInfo.CurrentUICulture.Name;
        global::Aero.Core.Railway.Result<DocsPage?, AeroError> result;

        if (DraftId is { } draftId)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            result = await _docsService.GetByIdAsync(draftId, cancellationToken);
        }
        else
        {
            // The localized route has already consumed both the culture and the "docs"
            // prefix. Treat the catch-all as a document path, not as another culture-aware
            // URL; valid path segments such as "api" can also be accepted by CultureInfo.
            var documentPath = Slug?.Trim().Trim('/');
            var pageSlug = string.IsNullOrWhiteSpace(documentPath) ? "docs" : $"docs/{documentPath}";
            result = await _docsService.GetPublishedBySlugAsync(pageSlug, RequestedCulture, cancellationToken);
        }

        if (result is not global::Aero.Core.Railway.Result<DocsPage?, AeroError>.Ok ok || ok.Value is null)
            return NotFound();

        MarkdownPage = ok.Value;
        RenderedCulture = MarkdownPage.Culture;
        IsCultureFallback = !string.Equals(RequestedCulture, RenderedCulture, StringComparison.OrdinalIgnoreCase);
        CanonicalUrl = BuildCultureUrl(RenderedCulture, MarkdownPage.Slug);
        AlternateLinks = await BuildAlternateLinksAsync(MarkdownPage, cancellationToken);
        CultureSwitcherLinks = BuildCultureSwitcherLinks(AlternateLinks);

        // ── Sidebar tree (full published hierarchy) ────────────────────
        var treeResult = await _docsTreeService.GetSidebarTreeAsync(MarkdownPage.SiteId, MarkdownPage.Id, publishedOnly: true, RenderedCulture, cancellationToken);
        if (treeResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsSidebarNode>, AeroError>.Ok treeOk)
        {
            SidebarTree = treeOk.Value.ToList();
        }

        // Keep the public navigation useful when a stale projection/cache or a
        // legacy record without a normalized culture makes the tree query empty.
        // The current page is already site- and publication-authorized, so this
        // fallback can safely rebuild the same hierarchy from the published set.
        if (SidebarTree.Count == 0)
        {
            var fallbackResult = await _docsService.GetAllAsync(cancellationToken);
            if (fallbackResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok fallbackOk)
            {
                var published = fallbackOk.Value
                    .Where(doc => doc.SiteId == MarkdownPage.SiteId)
                    .Where(doc => doc.PublicationState == ContentPublicationState.Published)
                    .ToList();

                var culturePublished = published
                    .Where(doc => string.IsNullOrWhiteSpace(doc.Culture)
                        || string.Equals(doc.Culture, RenderedCulture, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (culturePublished.Any(doc => string.Equals(doc.Slug, "docs", StringComparison.OrdinalIgnoreCase)))
                    published = culturePublished;
                else
                {
                    var rootCulture = published
                        .FirstOrDefault(doc => string.Equals(doc.Slug, "docs", StringComparison.OrdinalIgnoreCase))?.Culture;
                    if (!string.IsNullOrWhiteSpace(rootCulture))
                    {
                        published = published
                            .Where(doc => string.IsNullOrWhiteSpace(doc.Culture)
                                || string.Equals(doc.Culture, rootCulture, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                }

                SidebarTree = BuildSidebarTree(published, MarkdownPage.Id);
            }
        }

        // ── Child pages (for space overview feature cards) ─────────────
        // Check if this page is a space root (top-level child of "docs")
        if (MarkdownPage.ParentId is null)
        {
            // Page is the root "docs" page → children are spaces
            var childrenResult = await _docsService.GetChildrenAsync(MarkdownPage.Id, RenderedCulture, cancellationToken);
            if (childrenResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok childrenOk)
                ChildPages = childrenOk.Value.Where(c => c.PublicationState == ContentPublicationState.Published).ToList();
        }
        else
        {
            // Page may be a space root — check if it has published children
            var childrenResult = await _docsService.GetChildrenAsync(MarkdownPage.Id, RenderedCulture, cancellationToken);
            if (childrenResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok childrenOk)
                ChildPages = childrenOk.Value.Where(c => c.PublicationState == ContentPublicationState.Published).ToList();
        }

        var breadcrumbResult = await _docsTreeService.GetBreadcrumbsAsync(MarkdownPage.SiteId, MarkdownPage.Id, publishedOnly: true, RenderedCulture, cancellationToken);
        if (breadcrumbResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok breadcrumbOk)
            Breadcrumbs = breadcrumbOk.Value;

        // ── "On This Page" headings via Markdig AST ────────────────────
        OnThisPage = _docsTreeService.ExtractHeadings(MarkdownPage.MarkdownContent).ToList();

        // ── Capture active doc ID for sidebar highlighting ─────────────
        ViewData["ActiveDocId"] = MarkdownPage.Id;
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RenderedCulture;
        ViewData["IsCultureFallback"] = IsCultureFallback;
        ViewData["CultureSwitcherLinks"] = CultureSwitcherLinks;

        // ── Fine-grained cache tags (for invalidation) ────────────────
        HttpContext.Items["AeroCms.DocId"] = MarkdownPage.Id;
        HttpContext.Items["AeroCms.DocSlug"] = MarkdownPage.Slug;

        ApplyResponseCacheHeaders();
        return Page();
    }

    /// <summary>
    /// Prevents authenticated draft previews from being stored by browsers or intermediary caches.
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

        Response.Headers.CacheControl = "public,max-age=600";
    }

    private static List<DocsSidebarNode> BuildSidebarTree(IReadOnlyList<DocsPage> pages, long activeId)
    {
        var root = pages.FirstOrDefault(page => string.Equals(page.Slug, "docs", StringComparison.OrdinalIgnoreCase));
        if (root is null)
            return [];

        var childrenByParent = pages
            .GroupBy(page => page.ParentId ?? 0)
            .ToDictionary(group => group.Key, group => group.OrderBy(page => page.Order).ThenBy(page => page.Title).ToList());

        return BuildSidebarNodes(root.Id, 0, activeId, childrenByParent);
    }

    private static List<DocsSidebarNode> BuildSidebarNodes(
        long parentId,
        int depth,
        long activeId,
        IReadOnlyDictionary<long, List<DocsPage>> childrenByParent)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
            return [];

        var nodes = new List<DocsSidebarNode>();
        foreach (var page in children)
        {
            var childNodes = BuildSidebarNodes(page.Id, depth + 1, activeId, childrenByParent);
            var isActive = page.Id == activeId;
            nodes.Add(new DocsSidebarNode
            {
                Id = page.Id,
                Title = page.Title,
                Slug = page.Slug,
                Order = page.Order,
                Depth = depth,
                IsActive = isActive,
                IsExpanded = isActive || childNodes.Any(node => node.IsActive || node.IsExpanded),
                Children = childNodes
            });
        }

        return nodes;
    }

    /// <summary>
    /// Builds localized links from published members of the page's translation group.
    /// </summary>
    private async Task<IReadOnlyList<AlternateDocLink>> BuildAlternateLinksAsync(DocsPage page, CancellationToken cancellationToken)
    {
        var variantsResult = await _docsService.ListCultureVariantsAsync(page.Id, cancellationToken);
        var variants = variantsResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok
            ? ok.Value
            : [page];

        var publishedVariants = variants
            .Where(variant => variant.PublicationState == ContentPublicationState.Published)
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Culture))
            .GroupBy(variant => variant.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (publishedVariants.Count == 0 && page.PublicationState == ContentPublicationState.Published)
            publishedVariants.Add(page);

        var links = publishedVariants
            .Select(variant => new AlternateDocLink(
                variant.Culture.ToLowerInvariant(),
                BuildCultureUrl(variant.Culture, variant.Slug)))
            .ToList();

        var defaultCulture = HttpContext.Features.Get<IAeroSiteSlice>()?.DefaultCulture ?? page.Culture;
        var defaultVariant = publishedVariants.FirstOrDefault(variant =>
            string.Equals(variant.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase));

        if (defaultVariant is not null)
            links.Add(new AlternateDocLink("x-default", BuildCultureUrl(defaultVariant.Culture, defaultVariant.Slug)));

        return links;
    }

    /// <summary>
    /// Converts alternate links into one switcher entry per culture and omits <c>x-default</c>.
    /// </summary>
    private IReadOnlyList<CultureSwitcherLink> BuildCultureSwitcherLinks(IReadOnlyList<AlternateDocLink> alternateLinks)
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
    /// Builds an absolute URL from the current request origin and a localized CMS path.
    /// </summary>
    private string BuildCultureUrl(string culture, string? slug)
        => UriHelper.BuildAbsolute(
            Request.Scheme,
            Request.Host,
            Request.PathBase,
            AeroCultureRoute.BuildCulturePath(culture, slug));

    /// <summary>
    /// Describes an HTML alternate-language relation for a documentation page.
    /// </summary>
    /// <param name="Hreflang">The lower-case culture tag or <c>x-default</c>.</param>
    /// <param name="Href">The absolute localized page URL.</param>
public sealed record AlternateDocLink(string Hreflang, string Href);
}
