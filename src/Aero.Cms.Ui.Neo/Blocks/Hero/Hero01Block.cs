using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.Hero;

/// <summary>
/// Represents a class for Hero01Block.
/// </summary>
[BlockMetadata(
    "aero.hero.01",
    "Hero 01",
    Category = "Neo",
    Icon = "sparkles",
    SortOrder = 10,
    SchemaVersion = 1)]
public sealed class Hero01Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "aero.hero.01";
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Eyebrow.
    /// </summary>
public string Eyebrow { get; set; } = "Introducing NeoUI v3";
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Build beautiful Blazor apps";
        /// <summary>
    /// Gets or sets the Highlight.
    /// </summary>
public string Highlight { get; set; } = "faster than ever";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } =
        "Aero Hero Block.";
        /// <summary>
    /// Gets or sets the Primary Text.
    /// </summary>
public string PrimaryText { get; set; } = "Get started for free";
        /// <summary>
    /// Gets or sets the Primary Url.
    /// </summary>
public string PrimaryUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Secondary Text.
    /// </summary>
public string SecondaryText { get; set; } = "View on GitHub";
        /// <summary>
    /// Gets or sets the Secondary Url.
    /// </summary>
public string SecondaryUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Trust Markers.
    /// </summary>
public List<string> TrustMarkers { get; set; } =
    [
        "Free & open source",
        ".NET 8+ compatible",
        "Dark mode included",
        "100+ components"
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
