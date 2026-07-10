using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.StatsRow;

/// <summary>
/// Represents a class for StatsRowBlock.
/// </summary>
[BlockMetadata(
    "neo.stats.row",
    "Status / Social Row",
    Category = "Neo",
    Icon = "bar-chart-3",
    SortOrder = 50,
    SchemaVersion = 1)]
public sealed class StatsRowBlock : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "neo.stats.row";
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Stats.
    /// </summary>
public List<StatItem> Stats { get; set; } =
    [
        new("10,000+", "Happy Users"),
        new("$2M+", "ARR Generated"),
        new("99.9%", "Uptime SLA"),
        new("4.9/5", "Average Rating"),
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a record for StatItem.
/// </summary>
public record StatItem(string Value, string Label);
