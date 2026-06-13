using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Stats;

/// <summary>
/// HyperUI Stats 2 — stat cards separated by dividers.
/// Source: hyperui/public/examples/marketing/stats/2.html.
/// </summary>
[BlockMetadata(
    "hyper.stats.2",
    "Stats 2",
    Category = "Hyper",
    Icon = "bar-chart",
    SortOrder = 64,
    SchemaVersion = 1)]
public sealed class Stats2Block : BlockBase
{
    public const string BlockTypeId = "hyper.stats.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Trusted by eCommerce Businesses";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Ratione dolores laborum labore provident impedit esse recusandae facere libero harum sequi.";
    public List<StatItem> Stats { get; set; } = Stats1Block.DefaultStats.Select(CloneStat).ToList();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static StatItem CloneStat(StatItem stat) => new()
    {
        Label = stat.Label,
        Value = stat.Value
    };
}
