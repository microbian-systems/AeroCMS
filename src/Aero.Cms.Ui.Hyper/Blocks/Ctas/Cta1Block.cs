using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// HyperUI CTA 1 — side-by-side with image (text left, image right, emerald CTA).
/// Source: hyperui/public/examples/marketing/ctas/1.html, 1-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.ctas.1",
    "CTA 1",
    Category = "Hyper",
    Icon = "megaphone",
    SortOrder = 66,
    SchemaVersion = 1)]
public sealed class Cta1Block : BlockBase
{
    public const string BlockTypeId = "hyper.ctas.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Et, egestas tempus tellus etiam sed. Quam a scelerisque amet ullamcorper eu enim et fermentum, augue. Aliquet amet volutpat quisque ut interdum tincidunt duis.";
    public string CtaText { get; set; } = "Get Started Today";
    public string CtaUrl { get; set; } = "#";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1464582883107-8adf2dca8a9f?auto=format&fit=crop&q=80&w=1160";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
