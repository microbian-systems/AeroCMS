namespace Aero.Cms.Abstractions.Blocks.Layout;

/// <summary>
/// Represents a content region within a page layout that contains a collection of columns.
/// </summary>
public class LayoutRegion
{
    /// <summary>
    /// Gets or sets the name identifier for this region (e.g., "Header", "MainContent", "Footer").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display order of this region within the page layout.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the collection of columns within this region.
    /// </summary>
    public List<LayoutColumn> Columns { get; set; } = [];
}
