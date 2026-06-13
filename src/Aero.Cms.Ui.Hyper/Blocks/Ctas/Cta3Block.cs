using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// HyperUI CTA 3 — side-by-side with image and curved top-left corner (emerald CTA).
/// Source: hyperui/public/examples/marketing/ctas/3.html, 3-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.ctas.3",
    "CTA 3",
    Category = "Hyper",
    Icon = "megaphone",
    SortOrder = 68,
    SchemaVersion = 1)]
public sealed class Cta3Block : BlockBase
{
    public const string BlockTypeId = "hyper.ctas.3";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Et, egestas tempus tellus etiam sed. Quam a scelerisque amet ullamcorper eu enim et fermentum, augue. Aliquet amet volutpat quisque ut interdum tincidunt duis.";
    public string CtaText { get; set; } = "Get Started Today";
    public string CtaUrl { get; set; } = "#";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1484959014842-cd1d967a39cf?auto=format&fit=crop&q=80&w=1160";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
