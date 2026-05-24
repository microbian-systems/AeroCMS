using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Modules.Docs.Areas.Docs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var pageSlug = string.IsNullOrWhiteSpace(Slug) ? "docs" : "docs/" + Slug.TrimStart('/');

        var result = await _docsService.GetPublishedBySlugAsync(pageSlug, cancellationToken);

        if (result is not global::Aero.Core.Railway.Result<DocsPage?, AeroError>.Ok ok || ok.Value is null)
            return NotFound();

        MarkdownPage = ok.Value;

        // ── Sidebar tree (full published hierarchy) ────────────────────
        var treeResult = await _docsTreeService.GetSidebarTreeAsync(MarkdownPage.SiteId, MarkdownPage.Id, publishedOnly: true, cancellationToken);
        if (treeResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsSidebarNode>, AeroError>.Ok treeOk)
        {
            SidebarTree = treeOk.Value.ToList();
        }

        // ── Child pages (for space overview feature cards) ─────────────
        // Check if this page is a space root (top-level child of "docs")
        if (MarkdownPage.ParentId is null)
        {
            // Page is the root "docs" page → children are spaces
            var childrenResult = await _docsService.GetChildrenAsync(MarkdownPage.Id, cancellationToken);
            if (childrenResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok childrenOk)
                ChildPages = childrenOk.Value.Where(c => c.PublicationState == ContentPublicationState.Published).ToList();
        }
        else
        {
            // Page may be a space root — check if it has published children
            var childrenResult = await _docsService.GetChildrenAsync(MarkdownPage.Id, cancellationToken);
            if (childrenResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok childrenOk)
                ChildPages = childrenOk.Value.Where(c => c.PublicationState == ContentPublicationState.Published).ToList();
        }

        var breadcrumbResult = await _docsTreeService.GetBreadcrumbsAsync(MarkdownPage.SiteId, MarkdownPage.Id, publishedOnly: true, cancellationToken);
        if (breadcrumbResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok breadcrumbOk)
            Breadcrumbs = breadcrumbOk.Value;

        // ── "On This Page" headings via Markdig AST ────────────────────
        OnThisPage = _docsTreeService.ExtractHeadings(MarkdownPage.MarkdownContent).ToList();

        // ── Capture active doc ID for sidebar highlighting ─────────────
        ViewData["ActiveDocId"] = MarkdownPage.Id;

        // ── Fine-grained cache tags (for invalidation) ────────────────
        HttpContext.Items["AeroCms.DocId"] = MarkdownPage.Id;
        HttpContext.Items["AeroCms.DocSlug"] = MarkdownPage.Slug;

        return Page();
    }
}
