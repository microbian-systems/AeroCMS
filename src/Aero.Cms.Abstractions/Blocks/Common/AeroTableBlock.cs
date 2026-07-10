using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A data table block for presenting structured information.
/// </summary>
[BlockMetadata("aero_table", "Aero Table", Category = "Aero")]
public class AeroTableBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_table";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Headers.
    /// </summary>
public List<AeroTableHeader> Headers { get; set; } = new();
        /// <summary>
    /// Gets or sets the Rows.
    /// </summary>
public List<AeroTableRow> Rows { get; set; } = new();
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroTableLayout Layout { get; set; } = AeroTableLayout.Simple;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroTableHeader.
/// </summary>
public class AeroTableHeader
{
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string? Label { get; set; }
}

/// <summary>
/// Represents a class for AeroTableRow.
/// </summary>
public class AeroTableRow
{
        /// <summary>
    /// Gets or sets the Cells.
    /// </summary>
public List<string> Cells { get; set; } = new();
}

/// <summary>
/// Defines an enumeration for AeroTableLayout.
/// </summary>
public enum AeroTableLayout
{
    Simple,
    Avatar,
    Bordered,
    Striped
}
