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
    public const string BlockTypeId = "hyper.cards.9";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Question about Rendering";
    public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Accusamus, accusantium temporibus iure delectus ut totam natus nesciunt ex? Ducimus, enim.";
    public string AvatarUrl { get; set; } = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&q=80&w=1160";
    public int CommentCount { get; set; } = 14;
    public string PostedBy { get; set; } = "John";
    public bool Solved { get; set; } = true;
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
