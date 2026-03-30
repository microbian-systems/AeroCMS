using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Core;
using Marten;

namespace Aero.Cms.Modules.Docs.Areas.Docs.Pages;

public class DocsIndexModel(IQuerySession session) : PageModel
{
    public IReadOnlyList<DocsPage> Chapters { get; private set; } = [];
    public Dictionary<long, List<DocsPage>> Sections { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var allPages = await session.Query<DocsPage>()
            .Where(p => p.PublicationState == ContentPublicationState.Published)
            .OrderBy(p => p.Order)
            .ToListAsync(cancellationToken);

        // Find the root "docs" page to identify top-level chapters
        var rootDoc = allPages.FirstOrDefault(p => p.Slug == "docs");
        var rootId = rootDoc?.Id;

        // Chapters are direct children of the root "docs" page
        Chapters = allPages.Where(p => p.ParentId == rootId).ToList();
        
        foreach (var chapter in Chapters)
        {
            // Sections are direct children of chapters
            Sections[chapter.Id] = allPages.Where(p => p.ParentId == chapter.Id).ToList();
        }
    }
}
