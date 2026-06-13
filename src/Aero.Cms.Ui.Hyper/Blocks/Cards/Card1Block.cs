using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 1 — blog/article card with avatar, title, byline, description, date, reading time.
/// Source: hyperui/public/examples/marketing/cards/1.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.1",
    "Card 1",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 94,
    SchemaVersion = 1)]
public sealed class Card1Block : BlockBase
{
    public const string BlockTypeId = "hyper.cards.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "How I built my first website with Nuxt, Tailwind CSS and Vercel";
    public string Description { get; set; } = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. At velit illum provident a, ipsa maiores deleniti consectetur nobis et eaque.";
    public string AuthorName { get; set; } = "John Doe";
    public string AvatarUrl { get; set; } = "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160";
    public string DateStr { get; set; } = "31/06/2025";
    public string ReadingTime { get; set; } = "12 minutes";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
