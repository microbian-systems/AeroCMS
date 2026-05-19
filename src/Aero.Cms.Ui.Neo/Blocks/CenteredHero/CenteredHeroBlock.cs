using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero;

[BlockMetadata(
    "neo.hero.centered",
    "Centered Hero",
    Category = "Neo",
    Icon = "sparkles",
    SortOrder = 10,
    SchemaVersion = 1)]
public sealed class CenteredHeroBlock : BlockBase
{
    public const string BlockTypeId = "neo.hero.centered";
    public override string BlockType => BlockTypeId;

    public string Eyebrow { get; set; } = "Introducing NeoUI v3";
    public string Title { get; set; } = "Build beautiful Blazor apps";
    public string Highlight { get; set; } = "faster than ever";
    public string Description { get; set; } =
        "100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed.";
    public string PrimaryText { get; set; } = "Get started for free";
    public string PrimaryUrl { get; set; } = "#";
    public string SecondaryText { get; set; } = "View on GitHub";
    public string SecondaryUrl { get; set; } = "#";
    public List<string> TrustMarkers { get; set; } =
    [
        "Free & open source",
        ".NET 8+ compatible",
        "Dark mode included",
        "100+ components"
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
