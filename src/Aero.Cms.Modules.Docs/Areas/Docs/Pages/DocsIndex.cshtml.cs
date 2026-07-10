using Aero.Cms.Modules.Docs.Areas.Docs.Models;
using Aero.Cms.Abstractions.Interfaces;
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
/// Represents a class for DocsIndexModel.
/// </summary>
[ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "DocsIndexPolicy")]
public class DocsIndexModel : PageModel
{
    private readonly IDocsService _docsService;
    private readonly IDocsTreeService _docsTreeService;

        /// <summary>
    /// Initializes a new instance of the <see cref="DocsIndexModel"/> class.
    /// </summary>
public DocsIndexModel(IDocsService docsService, IDocsTreeService docsTreeService)
    {
        _docsService = docsService;
        _docsTreeService = docsTreeService;
    }

        /// <summary>
    /// Gets or sets the Chapters.
    /// </summary>
public IReadOnlyList<DocsPage> Chapters { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Sections.
    /// </summary>
public Dictionary<long, List<DocsPage>> Sections { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Sidebar Tree.
    /// </summary>
public List<DocsSidebarNode> SidebarTree { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Requested Culture.
    /// </summary>
public string RequestedCulture { get; private set; } = "en-US";
        /// <summary>
    /// Gets or sets the Rendered Culture.
    /// </summary>
public string RenderedCulture { get; private set; } = "en-US";
        /// <summary>
    /// Gets or sets the Is Culture Fallback.
    /// </summary>
public bool IsCultureFallback { get; private set; }
        /// <summary>
    /// Gets or sets the Canonical Url.
    /// </summary>
public string CanonicalUrl { get; private set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alternate Links.
    /// </summary>
public IReadOnlyList<AlternateDocsIndexLink> AlternateLinks { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Culture Switcher Links.
    /// </summary>
public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        RequestedCulture = CultureInfo.CurrentUICulture.Name;
        RenderedCulture = RequestedCulture;
        CanonicalUrl = UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(RenderedCulture, "docs"));
        AlternateLinks = BuildSupportedCultureAlternates();
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RenderedCulture;
        ViewData["IsCultureFallback"] = IsCultureFallback;
        ViewData["CultureSwitcherLinks"] = CultureSwitcherLinks;
        var result = await _docsService.GetPublishedAsync(RequestedCulture, cancellationToken);

        if (result is not global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
        {
            Chapters = [];
            Sections = new Dictionary<long, List<DocsPage>>();
            return;
        }

        var allPages = ok.Value;

        // Find the root "docs" page to identify top-level chapters
        var rootDoc = allPages.FirstOrDefault(p => p.Slug == "docs");
        var defaultCulture = HttpContext.Features.Get<IAeroSiteSlice>()?.DefaultCulture ?? "en-US";
        if (rootDoc is null && !string.Equals(RequestedCulture, defaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackResult = await _docsService.GetPublishedAsync(defaultCulture, cancellationToken);
            if (fallbackResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok fallbackOk)
            {
                allPages = fallbackOk.Value;
                rootDoc = allPages.FirstOrDefault(p => p.Slug == "docs");
                RenderedCulture = rootDoc?.Culture ?? defaultCulture;
                IsCultureFallback = true;
            }
        }

        var rootId = rootDoc?.Id ?? 0;
        if (rootDoc is not null)
            RenderedCulture = rootDoc.Culture;

        // Chapters are direct children of the root "docs" page
        Chapters = allPages.Where(p => p.ParentId == rootId).ToList();

        foreach (var chapter in Chapters)
        {
            // Sections are direct children of chapters
            Sections[chapter.Id] = allPages.Where(p => p.ParentId == chapter.Id).ToList();
        }

        var sidebarResult = await _docsTreeService.GetSidebarTreeAsync(rootDoc?.SiteId ?? 0, activeId: 0, publishedOnly: true, RenderedCulture, cancellationToken);
        SidebarTree = sidebarResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsSidebarNode>, AeroError>.Ok treeOk
            ? treeOk.Value.ToList()
            : [];

        CanonicalUrl = UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(RenderedCulture, "docs"));
        AlternateLinks = await BuildAlternateLinksAsync(rootDoc, cancellationToken);
        CultureSwitcherLinks = BuildCultureSwitcherLinks(rootDoc);
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RenderedCulture;
        ViewData["IsCultureFallback"] = IsCultureFallback;
        ViewData["CultureSwitcherLinks"] = CultureSwitcherLinks;
    }

    private IReadOnlyList<CultureSwitcherLink> BuildCultureSwitcherLinks(DocsPage? rootDoc)
    {
        if (rootDoc is null)
            return [];

        return [CultureSwitcher.CreateLink(
            rootDoc.Culture.ToLowerInvariant(),
            UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(rootDoc.Culture, rootDoc.Slug)),
            true)];
    }

    private async Task<IReadOnlyList<AlternateDocsIndexLink>> BuildAlternateLinksAsync(DocsPage? rootDoc, CancellationToken cancellationToken)
    {
        if (rootDoc is null)
            return BuildSupportedCultureAlternates();

        var variantsResult = await _docsService.ListCultureVariantsAsync(rootDoc.Id, cancellationToken);
        var variants = variantsResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok
            ? ok.Value
            : [rootDoc];

        var publishedRoots = variants
            .Where(doc => doc.PublicationState == Aero.Cms.Abstractions.Enums.ContentPublicationState.Published)
            .Where(doc => string.Equals(doc.Slug, "docs", StringComparison.OrdinalIgnoreCase))
            .GroupBy(doc => doc.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (publishedRoots.Count == 0)
            publishedRoots.Add(rootDoc);

        var links = publishedRoots
            .Select(doc => new AlternateDocsIndexLink(
                doc.Culture.ToLowerInvariant(),
                UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(doc.Culture, "docs"))))
            .ToList();

        var defaultCulture = HttpContext.Features.Get<IAeroSiteSlice>()?.DefaultCulture ?? rootDoc.Culture;
        var defaultRoot = publishedRoots.FirstOrDefault(doc =>
            string.Equals(doc.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase));

        if (defaultRoot is not null)
        {
            links.Add(new AlternateDocsIndexLink(
                "x-default",
                UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(defaultRoot.Culture, "docs"))));
        }

        return links;
    }

    private IReadOnlyList<AlternateDocsIndexLink> BuildSupportedCultureAlternates()
    {
        var site = HttpContext.Features.Get<IAeroSiteSlice>();
        var defaultCulture = AeroCultureRoute.NormalizeCultureOrDefault(site?.DefaultCulture, RenderedCulture);
        var supportedCultures = AeroCultureRoute.NormalizeSupportedCultures(site?.SupportedCultures, defaultCulture);

        var links = supportedCultures
            .Select(culture => new AlternateDocsIndexLink(
                culture.ToLowerInvariant(),
                UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(culture, "docs"))))
            .ToList();

        links.Add(new AlternateDocsIndexLink(
            "x-default",
            UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, AeroCultureRoute.BuildCulturePath(defaultCulture, "docs"))));

        return links;
    }

        /// <summary>
    /// Represents a record for AlternateDocsIndexLink.
    /// </summary>
public sealed record AlternateDocsIndexLink(string Hreflang, string Href);
}
