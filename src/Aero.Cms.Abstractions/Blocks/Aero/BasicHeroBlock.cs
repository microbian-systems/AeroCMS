using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for BasicHeroBlock.
/// </summary>
[BlockMetadata(
    "aero.hero.basic",
    "Basic Hero",
    Category = "Aero UI",
    Icon = "layout",
    SortOrder = 20,
    SchemaVersion = 1)]
public sealed class BasicHeroBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero.hero.basic";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Welcome";
        /// <summary>
    /// Gets or sets the Subtitle.
    /// </summary>
public string Subtitle { get; set; } = "Your message goes here.";
        /// <summary>
    /// Gets or sets the Background Image Url.
    /// </summary>
public string? BackgroundImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string? CtaText { get; set; }
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string? CtaUrl { get; set; }

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
