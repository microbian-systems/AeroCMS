using Aero.Core.Entities;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// Represents a horizontal row that can contain multiple columns.
/// </summary>
[BlockMetadata("columns", "Columns", Category = "Layout")]
public class ColumnsBlock : BlockBase
{
    public override string BlockType => "columns";

    /// <summary>
    /// The collection of columns within this row.
    /// </summary>
    public List<ColumnItem> Columns { get; set; } = new();

    /// <summary>
    /// Optional CSS classes for the row container.
    /// </summary>
    public string? RowClasses { get; set; }

    /// <summary>
    /// Gap between columns in tailwind or css units.
    /// </summary>
    public string? Gap { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A column within a <see cref="ColumnsBlock"/>.
/// </summary>
public class ColumnItem : Entity
{
    /// <summary>
    /// Grid span for this column (typical 1-12 scale).
    /// </summary>
    public int Span { get; set; } = 12;

    /// <summary>
    /// Optional CSS classes for the column.
    /// </summary>
    public string? ColumnClasses { get; set; }

    /// <summary>
    /// The blocks contained within this column.
    /// </summary>
    public List<BlockBase> Blocks { get; set; } = new();
}

/// <summary>
/// A specialized columns block specifically for display as cards.
/// </summary>
[BlockMetadata("cards", "Cards", Category = "Layout")]
public sealed class CardBlock : ColumnsBlock
{
    public override string BlockType => "cards";

    /// <summary>
    /// Whether to display an image at the top of the cards.
    /// </summary>
    public bool ShowImageOnTop { get; set; } = true;

    /// <summary>
    /// Specific style name for the card (e.g., elevated, flat, bordered).
    /// </summary>
    public string? CardStyle { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
