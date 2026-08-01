namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// Represents one site-scoped documentation page in the rendered sidebar hierarchy.
/// </summary>
public sealed class DocsSidebarNode
{
    /// <summary>
    /// Gets or sets the backing documentation page identifier.
    /// </summary>
public long Id { get; set; }

    /// <summary>
    /// Gets or sets the text displayed for the page.
    /// </summary>
public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the route slug for the page.
    /// </summary>
public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page's sibling display order.
    /// </summary>
public int Order { get; set; }

    /// <summary>
    /// Gets or sets the zero-based depth below the virtual <c>docs</c> root.
    /// </summary>
public int Depth { get; set; }

    /// <summary>
    /// Gets or sets whether this node identifies the requested page.
    /// </summary>
public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether this node is active or contains the active node.
    /// </summary>
public bool IsExpanded { get; set; }

    /// <summary>
    /// Gets or sets the node's ordered child hierarchy.
    /// </summary>
public List<DocsSidebarNode> Children { get; set; } = [];
}
