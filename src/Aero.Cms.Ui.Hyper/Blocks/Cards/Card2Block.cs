using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 2 — image card with title and description.
/// Source: hyperui/public/examples/marketing/cards/2.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.2",
    "Card 2",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 95,
    SchemaVersion = 1)]
public sealed class Card2Block : BlockBase
{
    public const string BlockTypeId = "hyper.cards.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Lorem, ipsum dolor.";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Magni reiciendis sequi ipsam incidunt.";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1605721911519-3dfeb3be25e7?auto=format&fit=crop&q=80&w=1160";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
