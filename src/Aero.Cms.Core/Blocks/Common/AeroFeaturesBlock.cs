using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A block for displaying features or services in various layouts (grid, cards, centered).
/// </summary>
[BlockMetadata("aero_features", "Aero Features", Category = "Aero")]
public class AeroFeaturesBlock : BlockBase
{
    public override string BlockType => "aero_features";

    public string? Title { get; set; }

    public string? SubTitle { get; set; }

    public AeroFeaturesLayout Layout { get; set; } = AeroFeaturesLayout.Simple;

    public List<AeroFeatureItem> Items { get; set; } = new();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroFeatureItem
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; } // SVG path or name
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
}

public enum AeroFeaturesLayout
{
    Simple,
    Centered,
    Cards,
    GridList,
    Media
}
