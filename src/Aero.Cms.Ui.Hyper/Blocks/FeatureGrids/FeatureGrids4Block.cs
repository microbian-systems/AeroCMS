using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// HyperUI Feature Grid 4 — two-column feature grid with bordered cards and icon support.
/// Source: hyperui/public/examples/marketing/feature-grids/4.html.
/// </summary>
[BlockMetadata(
    "hyper.feature-grids.4",
    "Feature Grid 4",
    Category = "Hyper",
    Icon = "layout-grid",
    SortOrder = 23,
    SchemaVersion = 1)]
public sealed class FeatureGrids4Block : BlockBase
{
    public const string BlockTypeId = "hyper.feature-grids.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Feature Grid 4";
    public string Description { get; set; } = "Features that matter.";
    public List<FeatureGrids4Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

    public static readonly List<FeatureGrids4Item> DefaultItems =
    [
        new() { Title = "High performance", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Aut accusamus consectetur eius quo ea ipsa illum eos amet velit assumenda.", SvgPath = "m3.75 13.5 10.5-11.25L12 10.5h8.25L9.75 21.75 12 13.5H3.75Z" },
        new() { Title = "Enterprise security", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Est optio fugit eos quia ipsam ullam pariatur iusto accusantium possimus distinctio?", SvgPath = "M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" },
        new() { Title = "Highly configurable", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Omnis ea cum harum corporis debitis. Nobis officia dignissimos aliquam praesentium quod?", SvgPath = "M6 13.5V3.75m0 9.75a1.5 1.5 0 0 1 0 3m0-3a1.5 1.5 0 0 0 0 3m0 3.75V16.5m12-3V3.75m0 9.75a1.5 1.5 0 0 1 0 3m0-3a1.5 1.5 0 0 0 0 3m0 3.75V16.5m-6-9V3.75m0 3.75a1.5 1.5 0 0 1 0 3m0-3a1.5 1.5 0 0 0 0 3m0 9.75V10.5" },
        new() { Title = "Advanced reporting", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Omnis ea cum harum corporis debitis. Nobis officia dignissimos aliquam praesentium quod?", SvgPath = "M3.75 3v11.25A2.25 2.25 0 0 0 6 16.5h2.25M3.75 3h-1.5m1.5 0h16.5m0 0h1.5m-1.5 0v11.25A2.25 2.25 0 0 1 18 16.5h-2.25m-7.5 0h7.5m-7.5 0-1 3m8.5-3 1 3m0 0 .5 1.5m-.5-1.5h-9.5m0 0-.5 1.5M9 11.25v1.5M12 9v3.75m3-6v6" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FeatureGrids4Item CloneItem(FeatureGrids4Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        SvgPath = item.SvgPath
    };
}

public sealed class FeatureGrids4Item
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SvgPath { get; set; } = "";
}
