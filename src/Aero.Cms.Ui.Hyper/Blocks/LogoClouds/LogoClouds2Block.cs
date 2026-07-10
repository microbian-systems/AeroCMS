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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.logo-clouds.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Trusted by many";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit. Sed voluptas delectus alias magni velit! Dicta corrupti dignissimos dolor consequatur illum tempore consectetur hic a cupiditate sunt quam, earum nisi aperiam.";
        /// <summary>
    /// Gets or sets the Logo Items.
    /// </summary>
public List<LogoCloudsLogoItem> LogoItems { get; set; } = LogoCloudsDefaults.CloneDefaults();

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
