using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 4 — dashed border card with icon, title, and hover-reveal description.
/// Source: hyperui/public/examples/marketing/cards/4.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.4",
    "Card 4",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 97,
    SchemaVersion = 1)]
public sealed class Card4Block : BlockBase
{
    public const string BlockTypeId = "hyper.cards.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Go around the world";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Cupiditate, praesentium voluptatem omnis atque culpa repellendus.";
    public string SvgIcon { get; set; } = "M3.055 11H5a2 2 0 012 2v1a2 2 0 002 2 2 2 0 012 2v2.945M8 3.935V5.5A2.5 2.5 0 0010.5 8h.5a2 2 0 012 2 2 2 0 104 0 2 2 0 012-2h1.064M15 20.488V18a2 2 0 012-2h3.064M21 12a9 9 0 11-18 0 9 9 0 0118 0z";
    public string CtaText { get; set; } = "Read more";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
