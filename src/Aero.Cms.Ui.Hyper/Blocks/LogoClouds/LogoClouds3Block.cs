using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// HyperUI Logo Clouds 3 — left-aligned title + subtitle + rounded grid with bg-gray-100 cells.
/// Source: hyperui/public/examples/marketing/logo-clouds/3.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.logo-clouds.3",
    "Logo Clouds 3",
    Category = "Hyper",
    Icon = "layers",
    SortOrder = 75,
    SchemaVersion = 1)]
public sealed class LogoClouds3Block : BlockBase
{
    public const string BlockTypeId = "hyper.logo-clouds.3";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Trusted by many";
    public string Description { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit. Sed voluptas delectus alias magni velit! Dicta corrupti dignissimos dolor consequatur illum tempore consectetur hic a cupiditate sunt quam, earum nisi aperiam.";
    public List<LogoCloudsLogoItem> LogoItems { get; set; } = LogoCloudsDefaults.CloneDefaults();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
