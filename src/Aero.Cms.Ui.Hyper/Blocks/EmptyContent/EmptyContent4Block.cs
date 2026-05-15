using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 4 — "Explore more" with link cards and back to shopping CTA.
/// Source: hyperui/public/examples/marketing/empty-content/4.html + 4-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.4",
    "Empty Content 4",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 121,
    SchemaVersion = 1)]
public sealed class EmptyContent4Block : BlockBase
{
    public const string BlockTypeId = "hyper.empty-content.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Explore more";
    public string Description { get; set; } = "This section doesn't have content right now. Discover related topics and inspiration instead.";
    public string CtaText { get; set; } = "Back to Shopping";
    public string CtaUrl { get; set; } = "#";
    public List<EmptyContentLink> Links { get; set; } = DefaultLinks.Select(CloneLink).ToList();

    public static readonly List<EmptyContentLink> DefaultLinks =
    [
        new() { Title = "Featured Collection", Description = "Browse our curated selection", Url = "#" },
        new() { Title = "Latest Trends", Description = "See what's trending now", Url = "#" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static EmptyContentLink CloneLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        Url = l.Url
    };
}

public sealed class EmptyContentLink
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "#";
}
