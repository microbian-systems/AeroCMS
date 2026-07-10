using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Stats;

/// <summary>
/// HyperUI Stats 1 — bordered stat cards with rounded corners.
/// Source: hyperui/public/examples/marketing/stats/1.html.
/// </summary>
[BlockMetadata(
    "hyper.stats.1",
    "Stats 1",
    Category = "Hyper",
    Icon = "bar-chart",
    SortOrder = 63,
    SchemaVersion = 1)]
public sealed class Stats1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.stats.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Trusted by eCommerce Businesses";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Ratione dolores laborum labore provident impedit esse recusandae facere libero harum sequi.";
        /// <summary>
    /// Gets or sets the Stats.
    /// </summary>
public List<StatItem> Stats { get; set; } = DefaultStats.Select(CloneStat).ToList();

        /// <summary>
    /// DefaultStats.
    /// </summary>
public static readonly List<StatItem> DefaultStats =
    [
        new() { Label = "Total Sales", Value = "$4.8m" },
        new() { Label = "Official Addons", Value = "24" },
        new() { Label = "Total Addons", Value = "86" },
        new() { Label = "Downloads", Value = "86k" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static StatItem CloneStat(StatItem stat) => new()
    {
        Label = stat.Label,
        Value = stat.Value
    };
}
