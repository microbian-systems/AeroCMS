using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 2 — logo + social row, link columns, and copyright.
/// Source: hyperui/public/examples/marketing/footers/2.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.2",
    "Footer 2",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 41,
    SchemaVersion = 1)]
public sealed class Footer2Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
        /// <summary>
    /// Gets or sets the Link Columns.
    /// </summary>
public List<FooterLinkColumn> LinkColumns { get; set; } = FooterDefaults.DefaultLinkColumns4.Select(FooterDefaults.CloneColumn).ToList();
        /// <summary>
    /// Gets or sets the Copyright.
    /// </summary>
public string Copyright { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
