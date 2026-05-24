namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// A node in the docs sidebar tree. Rendered by
/// <c>_DocsSidebar.cshtml</c> with Alpine.js expand/collapse.
/// </summary>
public sealed class DocsSidebarNode
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Order { get; set; }
    public int Depth { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpanded { get; set; }
    public List<DocsSidebarNode> Children { get; set; } = [];
}
