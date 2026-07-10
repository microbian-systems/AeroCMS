using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero;

/// <summary>
/// Represents a class for SplitHeroBlock.
/// </summary>
[BlockMetadata(
    "neo.hero.split",
    "Hero Split Layout",
    Category = "Neo",
    Icon = "layout-dashboard",
    SortOrder = 20,
    SchemaVersion = 1)]
public sealed class SplitHeroBlock : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "neo.hero.split";
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Eyebrow.
    /// </summary>
public string Eyebrow { get; set; } = "New — v2.0 is here";
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Build better products, ship faster";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } =
        "The all-in-one platform that helps your team design, develop, and deliver exceptional digital experiences without the complexity.";
        /// <summary>
    /// Gets or sets the Primary Text.
    /// </summary>
public string PrimaryText { get; set; } = "Get started free";
        /// <summary>
    /// Gets or sets the Primary Url.
    /// </summary>
public string PrimaryUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Secondary Text.
    /// </summary>
public string SecondaryText { get; set; } = "Watch demo";
        /// <summary>
    /// Gets or sets the Secondary Url.
    /// </summary>
public string SecondaryUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Footnote.
    /// </summary>
public string Footnote { get; set; } = "No credit card required · Free 14-day trial";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
