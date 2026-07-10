using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// HyperUI Feature Grids 1 — 3-column grid of feature cards with icons.
/// Source: hyperui/public/examples/marketing/feature-grids/1.html.
/// </summary>
[BlockMetadata(
    "hyper.feature-grids.1",
    "Feature Grid 1",
    Category = "Hyper",
    Icon = "layout-grid",
    SortOrder = 20,
    SchemaVersion = 1)]
public sealed class FeatureGrids1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.feature-grids.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Features for growth";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Veritatis tenetur, nemo quam voluptas sunt impedit dolorem asperiores aliquid doloribus fugit.";
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<FeatureGrids1Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

        /// <summary>
    /// DefaultItems.
    /// </summary>
public static readonly List<FeatureGrids1Item> DefaultItems =
    [
        new()
        {
            Icon = "M3.75 13.5l10.5-11.25L12 10.5h8.25L9.75 21.75 12 13.5H3.75Z",
            Title = "High performance",
            Description = "Lightning-quick load times optimized for every device"
        },
        new()
        {
            Icon = "M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25Z",
            Title = "Enterprise security",
            Description = "Enterprise-grade security built into every layer"
        },
        new()
        {
            Icon = "M6 13.5V3.75m0 9.75a1.5 1.5 0 010 3m0-3a1.5 1.5 0 000 3m0 3.75V16.5m12-3V3.75m0 9.75a1.5 1.5 0 010 3m0-3a1.5 1.5 0 000 3m0 3.75V16.5m-6-9V3.75m0 3.75a1.5 1.5 0 010 3m0-3a1.5 1.5 0 000 3m0 9.75V10.5",
            Title = "Highly configurable",
            Description = "Adapt every aspect to match your brand and needs"
        }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FeatureGrids1Item CloneItem(FeatureGrids1Item item) => new()
    {
        Icon = item.Icon,
        Title = item.Title,
        Description = item.Description,
        LinkUrl = item.LinkUrl
    };
}

/// <summary>
/// Represents a class for FeatureGrids1Item.
/// </summary>
public sealed class FeatureGrids1Item
{
        /// <summary>
    /// Gets or sets the Icon.
    /// </summary>
public string Icon { get; set; } = "";
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "";
        /// <summary>
    /// Gets or sets the Link Url.
    /// </summary>
public string? LinkUrl { get; set; }
}
