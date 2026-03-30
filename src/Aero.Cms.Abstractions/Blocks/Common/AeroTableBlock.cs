using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A data table block for presenting structured information.
/// </summary>
[BlockMetadata("aero_table", "Aero Table", Category = "Aero")]
public class AeroTableBlock : BlockBase
{
    public override string BlockType => "aero_table";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroTableHeader> Headers { get; set; } = new();
    public List<AeroTableRow> Rows { get; set; } = new();
    public AeroTableLayout Layout { get; set; } = AeroTableLayout.Simple;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroTableHeader
{
    public string? Label { get; set; }
}

public class AeroTableRow
{
    public List<string> Cells { get; set; } = new();
}

public enum AeroTableLayout
{
    Simple,
    Avatar,
    Bordered,
    Striped
}
