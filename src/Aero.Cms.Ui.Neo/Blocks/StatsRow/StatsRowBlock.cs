using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.StatsRow;

[BlockMetadata(
    "neo.stats.row",
    "Status / Social Row",
    Category = "Neo",
    Icon = "bar-chart-3",
    SortOrder = 50,
    SchemaVersion = 1)]
public sealed class StatsRowBlock : BlockBase
{
    public const string BlockTypeId = "neo.stats.row";
    public override string BlockType => BlockTypeId;

    public List<StatItem> Stats { get; set; } =
    [
        new("10,000+", "Happy Users"),
        new("$2M+", "ARR Generated"),
        new("99.9%", "Uptime SLA"),
        new("4.9/5", "Average Rating"),
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public record StatItem(string Value, string Label);
