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
    public const string BlockTypeId = "hyper.cards.8";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Some Interesting Podcast Title";
    public string Description { get; set; } = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Ipsam nulla amet voluptatum sit rerum, atque, quo culpa ut necessitatibus eius suscipit eum accusamus, aperiam voluptas exercitationem facere aliquid fuga. Sint.";
    public string EpisodeBadge { get; set; } = "Episode #101";
    public string Duration { get; set; } = "48:32 minutes";
    public string Featuring { get; set; } = "Barry, Sandra and August";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
