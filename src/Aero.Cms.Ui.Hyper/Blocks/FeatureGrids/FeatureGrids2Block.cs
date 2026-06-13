using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// HyperUI Feature Grids 2 — 2-column layout with headline + stacked feature list.
/// Source: hyperui/public/examples/marketing/feature-grids/2.html.
/// </summary>
[BlockMetadata(
    "hyper.feature-grids.2",
    "Feature Grid 2",
    Category = "Hyper",
    Icon = "layout-grid",
    SortOrder = 21,
    SchemaVersion = 1)]
public sealed class FeatureGrids2Block : BlockBase
{
    public const string BlockTypeId = "hyper.feature-grids.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Features for growth";
    public string Description { get; set; } = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Veritatis tenetur, nemo quam voluptas sunt impedit dolorem asperiores aliquid doloribus fugit.";
    public List<FeatureGrids2Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

    public static readonly List<FeatureGrids2Item> DefaultItems =
    [
        new()
        {
            Icon = "M3.75 3v11.25A2.25 2.25 0 006 16.5h2.25M3.75 3h-1.5m1.5 0h16.5m0 0h1.5m-1.5 0v11.25A2.25 2.25 0 0118 16.5h-2.25m-7.5 0h7.5m-7.5 0-1 3m8.5-3 1 3m0 0 .5 1.5m-.5-1.5h-9.5m0 0-.5 1.5M9 11.25v1.5M12 9v3.75m3-6v6",
            Title = "Advanced reporting",
            Description = "Track metrics that matter with instant insights"
        },
        new()
        {
            Icon = "M18 18.72a9.094 9.094 0 003.741-.479 3 3 0 00-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0112 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 016 18.719m12 0a5.971 5.971 0 00-.941-3.197m0 0A5.995 5.995 0 0012 12.75a5.995 5.995 0 00-5.058 2.772m0 0a3 3 0 00-4.681 2.72 8.986 8.986 0 003.74.477m.94-3.197a5.971 5.971 0 00-.94 3.197M15 6.75a3 3 0 11-6 0 3 3 0 016 0Zm6 3a2.25 2.25 0 11-4.5 0 2.25 2.25 0 014.5 0Zm-13.5 0a2.25 2.25 0 11-4.5 0 2.25 2.25 0 014.5 0Z",
            Title = "Collaboration tools",
            Description = "Work together seamlessly across your organization"
        },
        new()
        {
            Icon = "M5.25 14.25h13.5m-13.5 0a3 3 0 01-3-3m3 3a3 3 0 100 6h13.5a3 3 0 100-6m-16.5-3a3 3 0 013-3h13.5a3 3 0 013 3m-19.5 0a4.5 4.5 0 01.9-2.7L5.737 5.1a3.375 3.375 0 012.7-1.35h7.126c1.062 0 2.062.5 2.7 1.35l2.587 3.45a4.5 4.5 0 01.9 2.7m0 0a3 3 0 01-3 3m0 3h.008v.008h-.008v-.008Zm0-6h.008v.008h-.008v-.008Zm-3 6h.008v.008h-.008v-.008Zm0-6h.008v.008h-.008v-.008Z",
            Title = "Third-party connectors",
            Description = "Connect with your favorite tools and services"
        }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FeatureGrids2Item CloneItem(FeatureGrids2Item item) => new()
    {
        Icon = item.Icon,
        Title = item.Title,
        Description = item.Description,
        LinkUrl = item.LinkUrl
    };
}

public sealed class FeatureGrids2Item
{
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? LinkUrl { get; set; }
}
