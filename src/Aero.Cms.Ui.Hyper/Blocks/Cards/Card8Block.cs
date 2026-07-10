using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 8 — podcast episode card with badge, title, description, duration, and featuring.
/// Source: hyperui/public/examples/marketing/cards/8.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.8",
    "Card 8",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 101,
    SchemaVersion = 1)]
public sealed class Card8Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.cards.8";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Some Interesting Podcast Title";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Ipsam nulla amet voluptatum sit rerum, atque, quo culpa ut necessitatibus eius suscipit eum accusamus, aperiam voluptas exercitationem facere aliquid fuga. Sint.";
        /// <summary>
    /// Gets or sets the Episode Badge.
    /// </summary>
public string EpisodeBadge { get; set; } = "Episode #101";
        /// <summary>
    /// Gets or sets the Duration.
    /// </summary>
public string Duration { get; set; } = "48:32 minutes";
        /// <summary>
    /// Gets or sets the Featuring.
    /// </summary>
public string Featuring { get; set; } = "Barry, Sandra and August";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
