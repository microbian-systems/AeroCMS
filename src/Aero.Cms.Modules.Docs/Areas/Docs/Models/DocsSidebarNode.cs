namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// A node in the docs sidebar tree. Rendered by
/// <c>_DocsSidebar.cshtml</c> with Alpine.js expand/collapse.
/// </summary>
public sealed class DocsSidebarNode
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public long Id { get; set; }
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; set; }
        /// <summary>
    /// Gets or sets the Depth.
    /// </summary>
public int Depth { get; set; }
        /// <summary>
    /// Gets or sets the Is Active.
    /// </summary>
public bool IsActive { get; set; }
        /// <summary>
    /// Gets or sets the Is Expanded.
    /// </summary>
public bool IsExpanded { get; set; }
        /// <summary>
    /// Gets or sets the Children.
    /// </summary>
public List<DocsSidebarNode> Children { get; set; } = [];
}
