namespace Aero.Cms.Modules.Pages.Models;

/// <summary>
/// Represents a column within a layout region that holds block placements.
/// Columns use a 12-column grid system where Width values range from 1 to 12.
/// </summary>
public class LayoutColumn
{
    /// <summary>
    /// Gets or sets the width of this column in a 12-column grid system.
    /// Valid values are 1 through 12, where 12 represents full width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the display order of this column within its parent region.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the collection of block placements contained within this column.
    /// </summary>
    public List<BlockPlacement> Blocks { get; set; } = [];
}
