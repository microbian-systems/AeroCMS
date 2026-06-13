using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// HyperUI Logo Clouds 2 — centered title + subtitle + grid of grayscale logo SVGs.
/// Source: hyperui/public/examples/marketing/logo-clouds/2.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.logo-clouds.2",
    "Logo Clouds 2",
    Category = "Hyper",
    Icon = "layers",
    SortOrder = 74,
    SchemaVersion = 1)]
public sealed class LogoClouds2Block : BlockBase
{
    public const string BlockTypeId = "hyper.logo-clouds.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Trusted by many";
    public string Description { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit. Sed voluptas delectus alias magni velit! Dicta corrupti dignissimos dolor consequatur illum tempore consectetur hic a cupiditate sunt quam, earum nisi aperiam.";
    public List<LogoCloudsLogoItem> LogoItems { get; set; } = LogoCloudsDefaults.CloneDefaults();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
