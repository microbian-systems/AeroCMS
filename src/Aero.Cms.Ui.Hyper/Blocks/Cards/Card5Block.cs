using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 5 — real estate card with price, address, and feature badges.
/// Source: hyperui/public/examples/marketing/cards/5.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.5",
    "Card 5",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 98,
    SchemaVersion = 1)]
public sealed class Card5Block : BlockBase
{
    public const string BlockTypeId = "hyper.cards.5";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "123 Wallaby Avenue, Park Road";
    public string Price { get; set; } = "$240,000";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1613545325278-f24b0cae1224?auto=format&fit=crop&q=80&w=1160";
    public List<Card5Feature> Features { get; set; } = DefaultFeatures.Select(CloneFeature).ToList();
    public string CtaUrl { get; set; } = "#";

    public static readonly List<Card5Feature> DefaultFeatures =
    [
        new() { Label = "Parking", Value = "2 spaces", SvgPath = "M8 14v3m4-3v3m4-3v3M3 21h18M3 10h18M3 7l9-4 9 4M4 10h16v11H4V10z" },
        new() { Label = "Bathroom", Value = "2 rooms", SvgPath = "M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" },
        new() { Label = "Bedroom", Value = "4 rooms", SvgPath = "M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Card5Feature CloneFeature(Card5Feature f) => new()
    {
        Label = f.Label,
        Value = f.Value,
        SvgPath = f.SvgPath
    };
}

public sealed class Card5Feature
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string SvgPath { get; set; } = "";
}
