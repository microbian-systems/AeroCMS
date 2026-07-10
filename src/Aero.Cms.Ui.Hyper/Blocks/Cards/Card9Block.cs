using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 9 — forum/question card with avatar, question, comments, posted by, and solved badge.
/// Source: hyperui/public/examples/marketing/cards/9.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.9",
    "Card 9",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 102,
    SchemaVersion = 1)]
public sealed class Card9Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.cards.9";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Question about Rendering";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Accusamus, accusantium temporibus iure delectus ut totam natus nesciunt ex? Ducimus, enim.";
        /// <summary>
    /// Gets or sets the Avatar Url.
    /// </summary>
public string AvatarUrl { get; set; } = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Comment Count.
    /// </summary>
public int CommentCount { get; set; } = 14;
        /// <summary>
    /// Gets or sets the Posted By.
    /// </summary>
public string PostedBy { get; set; } = "John";
        /// <summary>
    /// Gets or sets the Solved.
    /// </summary>
public bool Solved { get; set; } = true;
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
