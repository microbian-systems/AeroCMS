using Aero.Core.Entities;

namespace Aero.Cms.Core;

/// <summary>
/// Represents a navigation structure (menu) that can be displayed in the UI.
/// </summary>
public class Navigation : Entity
{
    /// <summary>
    /// Gets or sets the name of the navigation menu.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logical location/key for this navigation (e.g., "main", "footer").
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of navigation items.
    /// </summary>
    public List<NavigationItem> Items { get; set; } = [];
}

/// <summary>
/// Represents an individual item within a navigation menu.
/// </summary>
public class NavigationItem : Entity
{
    /// <summary>
    /// Gets or sets the display label for the navigation item.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target URL for the navigation item.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the optional ID of a page this item links to.
    /// </summary>
    public long? PageId { get; set; }

    /// <summary>
    /// Gets or sets the display order of the item.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the optional ID of the parent navigation item.
    /// </summary>
    public long? ParentId { get; set; }
}
