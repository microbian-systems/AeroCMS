namespace Aero.Cms.Abstractions.Blocks.Layout;

/// <summary>
/// Represents the placement of a content block within a layout column,
/// defining which block to render and its order within the column.
/// </summary>
public class BlockPlacement
{
    /// <summary>
    /// Gets or sets the unique identifier of the block being placed.
    /// </summary>
    public long BlockId { get; set; }

    /// <summary>
    /// Gets or sets the type identifier of the block (e.g., "TextBlock", "ImageBlock", "VideoBlock").
    /// </summary>
    public string BlockType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display order of this block placement within its parent column.
    /// </summary>
    public int Order { get; set; }
}
