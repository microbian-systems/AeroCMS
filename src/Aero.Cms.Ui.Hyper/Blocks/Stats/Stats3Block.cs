using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Stats;

/// <summary>
/// HyperUI Stats 3 — stat cards with blue background.
/// Source: hyperui/public/examples/marketing/stats/3.html.
/// </summary>
[BlockMetadata(
    "hyper.stats.3",
    "Stats 3",
    Category = "Hyper",
    Icon = "bar-chart",
    SortOrder = 65,
    SchemaVersion = 1)]
public sealed class Stats3Block : BlockBase
{
    public const string BlockTypeId = "hyper.stats.3";

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
