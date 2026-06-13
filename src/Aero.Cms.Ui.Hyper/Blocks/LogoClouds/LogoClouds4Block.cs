using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// HyperUI Logo Clouds 4 — rounded grid with bg-gray-100 aspect-video cells, no text.
/// Source: hyperui/public/examples/marketing/logo-clouds/4.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.logo-clouds.4",
    "Logo Clouds 4",
    Category = "Hyper",
    Icon = "layers",
    SortOrder = 76,
    SchemaVersion = 1)]
public sealed class LogoClouds4Block : BlockBase
{
    public const string BlockTypeId = "hyper.logo-clouds.4";

    public override string BlockType => BlockTypeId;

    public List<LogoCloudsLogoItem> LogoItems { get; set; } = LogoCloudsDefaults.CloneDefaults();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
