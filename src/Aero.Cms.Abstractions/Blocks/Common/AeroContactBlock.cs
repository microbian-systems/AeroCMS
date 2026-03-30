using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A contact block with forms, contact info, and map options.
/// </summary>
[BlockMetadata("aero_contact", "Aero Contact", Category = "Aero")]
public class AeroContactBlock : BlockBase
{
    public override string BlockType => "aero_contact";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroContactDetail>? Details { get; set; } = new();
    public string? FormActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public AeroContactLayout Layout { get; set; } = AeroContactLayout.Card;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroContactDetail
{
    public string? Icon { get; set; } // svg path or simple identifier
    public string? Label { get; set; }
    public string? Value { get; set; }
}

public enum AeroContactLayout
{
    Simple,
    Card,
    Grid,
    TwoColumn,
    Image,
    Map
}
