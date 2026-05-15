using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata(
    "aero.hero.basic",
    "Basic Hero",
    Category = "Aero UI",
    Icon = "layout",
    SortOrder = 20,
    SchemaVersion = 1)]
public sealed class BasicHeroBlock : BlockBase
{
    public override string BlockType => "aero.hero.basic";

    public string Title { get; set; } = "Welcome";
    public string Subtitle { get; set; } = "Your message goes here.";
    public string? BackgroundImageUrl { get; set; }
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
