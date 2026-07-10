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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.stats.3";

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
public List<StatItem> Stats { get; set; } = Stats1Block.DefaultStats.Select(CloneStat).ToList();

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
