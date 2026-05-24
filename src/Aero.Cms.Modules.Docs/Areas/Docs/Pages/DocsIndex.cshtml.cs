using Aero.Cms.Modules.Docs.Areas.Docs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Docs.Areas.Docs.Pages;

[ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
[OutputCache(PolicyName = "DocsIndexPolicy")]
public class DocsIndexModel : PageModel
{
    private readonly IDocsService _docsService;
    private readonly IDocsTreeService _docsTreeService;

    public DocsIndexModel(IDocsService docsService, IDocsTreeService docsTreeService)
    {
        _docsService = docsService;
        _docsTreeService = docsTreeService;
    }

    public IReadOnlyList<DocsPage> Chapters { get; private set; } = [];
    public Dictionary<long, List<DocsPage>> Sections { get; private set; } = [];
    public List<DocsSidebarNode> SidebarTree { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _docsService.GetPublishedAsync(cancellationToken);

        if (result is not global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
        {
            Chapters = [];
            Sections = new Dictionary<long, List<DocsPage>>();
            return;
        }

        var allPages = ok.Value;

        // Find the root "docs" page to identify top-level chapters
        var rootDoc = allPages.FirstOrDefault(p => p.Slug == "docs");
        var rootId = rootDoc?.Id ?? 0;

        // Chapters are direct children of the root "docs" page
        Chapters = allPages.Where(p => p.ParentId == rootId).ToList();

        foreach (var chapter in Chapters)
        {
            // Sections are direct children of chapters
            Sections[chapter.Id] = allPages.Where(p => p.ParentId == chapter.Id).ToList();
        }

        var sidebarResult = await _docsTreeService.GetSidebarTreeAsync(rootDoc?.SiteId ?? 0, activeId: 0, publishedOnly: true, cancellationToken);
        SidebarTree = sidebarResult is global::Aero.Core.Railway.Result<IReadOnlyList<DocsSidebarNode>, AeroError>.Ok treeOk
            ? treeOk.Value.ToList()
            : [];
    }
}
