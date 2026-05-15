using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// HyperUI Logo Clouds 1 — simple grid of grayscale logo SVGs.
/// Source: hyperui/public/examples/marketing/logo-clouds/1.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.logo-clouds.1",
    "Logo Clouds 1",
    Category = "Hyper",
    Icon = "layers",
    SortOrder = 73,
    SchemaVersion = 1)]
public sealed class LogoClouds1Block : BlockBase
{
    public const string BlockTypeId = "hyper.logo-clouds.1";

    public override string BlockType => BlockTypeId;

    public List<LogoCloudsLogoItem> LogoItems { get; set; } = LogoCloudsDefaults.CloneDefaults();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
