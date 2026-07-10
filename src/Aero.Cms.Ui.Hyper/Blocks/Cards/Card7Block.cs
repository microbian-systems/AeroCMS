using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 7 — portfolio card with image, company name, and category.
/// Source: hyperui/public/examples/marketing/cards/7.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.7",
    "Card 7",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 100,
    SchemaVersion = 1)]
public sealed class Card7Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.cards.7";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Company Name";
        /// <summary>
    /// Gets or sets the Subtitle.
    /// </summary>
public string Subtitle { get; set; } = "Branding / Signage";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1588515724527-074a7a56616c?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
