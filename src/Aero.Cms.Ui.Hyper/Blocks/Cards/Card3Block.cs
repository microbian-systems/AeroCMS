using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 3 — overlay card with background image, category label, name, and hover-reveal description.
/// Source: hyperui/public/examples/marketing/cards/3.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.3",
    "Card 3",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 96,
    SchemaVersion = 1)]
public sealed class Card3Block : BlockBase
{
    public const string BlockTypeId = "hyper.cards.3";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Tony Wayne";
    public string Subtitle { get; set; } = "Developer";
    public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Omnis perferendis hic asperiores quibusdam quidem voluptates doloremque reiciendis nostrum harum. Repudiandae?";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1603871165848-0aa92c869fa1?auto=format&fit=crop&q=80&w=1160";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
