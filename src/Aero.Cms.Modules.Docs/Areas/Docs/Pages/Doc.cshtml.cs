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

[ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "DocsPolicy")]
public class DocModel : PageModel
{
    private readonly IDocsService _docsService;
    private readonly IDocsTreeService _docsTreeService;

    public DocModel(IDocsService docsService, IDocsTreeService docsTreeService)
    {
        _docsService = docsService;
        _docsTreeService = docsTreeService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    public DocsPage? MarkdownPage { get; private set; }
    public IReadOnlyList<DocsPage> ChildPages { get; private set; } = [];
    public List<DocsSidebarNode> SidebarTree { get; private set; } = [];
    public List<HeadingItem> OnThisPage { get; private set; } = [];
    public IReadOnlyList<DocsPage> Breadcrumbs { get; private set; } = [];
    public string RequestedCulture { get; private set; } = SitesModel.DefaultCultureName;
    public string RenderedCulture { get; private set; } = SitesModel.DefaultCultureName;
    public bool IsCultureFallback { get; private set; }
    public string CanonicalUrl { get; private set; } = string.Empty;
    public IReadOnlyList<AlternateDocLink> AlternateLinks { get; private set; } = [];
    public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        RequestedCulture = CultureInfo.CurrentUICulture.Name;
        var slugWithoutCulture = AeroCultureRoute.StripLeadingCulture(Slug);
        var pageSlug = string.IsNullOrWhiteSpace(slugWithoutCulture) ? "docs" : "docs/" + slugWithoutCulture.TrimStart('/');

        var result = await _docsService.GetPublishedBySlugAsync(pageSlug, RequestedCulture, cancellationToken);

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

        return Page();
    }

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

    private string BuildCultureUrl(string culture, string? slug)
        => UriHelper.BuildAbsolute(
            Request.Scheme,
            Request.Host,
            Request.PathBase,
            AeroCultureRoute.BuildCulturePath(culture, slug));

    public sealed record AlternateDocLink(string Hreflang, string Href);
}
