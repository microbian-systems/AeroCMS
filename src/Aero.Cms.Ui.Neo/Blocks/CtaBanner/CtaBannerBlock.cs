using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.CtaBanner;

/// <summary>
/// Represents a class for CtaBannerBlock.
/// </summary>
[BlockMetadata(
    "neo.cta.banner",
    "CTA Banner",
    Category = "Neo",
    Icon = "megaphone",
    SortOrder = 30,
    SchemaVersion = 1)]
public sealed class CtaBannerBlock : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "neo.cta.banner";
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Start building for free today";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Join thousands of teams already using Acme to ship faster and smarter. No credit card required.";
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
public string SecondaryText { get; set; } = "Schedule a demo";
        /// <summary>
    /// Gets or sets the Secondary Url.
    /// </summary>
public string SecondaryUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
