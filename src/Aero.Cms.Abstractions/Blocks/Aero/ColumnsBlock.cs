using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for NeoColumnsBlock.
/// </summary>
[BlockMetadata("neo.layout.columns", "Columns", Category = "Layout", Icon = "columns-2", SortOrder = 90, SchemaVersion = 1)]
public sealed class NeoColumnsBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "neo.layout.columns";
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<ColumnItem> Items { get; set; } = [new(), new()];
        /// <summary>
    /// Gets or sets the Columns Per Row.
    /// </summary>
public int ColumnsPerRow { get; set; } = 2;
        /// <summary>
    /// Gets or sets the Gap.
    /// </summary>
public int Gap { get; set; } = 4;
        /// <summary>
    /// Gets or sets the Equal Height.
    /// </summary>
public bool EqualHeight { get; set; } = true;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for ColumnItem.
/// </summary>
public sealed class ColumnItem
{
        /// <summary>
    /// Gets or sets the Content.
    /// </summary>
public string Content { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Span.
    /// </summary>
public int Span { get; set; } = 6;
}
