using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 3 — logo + description + social, link columns grid, and copyright.
/// Source: hyperui/public/examples/marketing/footers/3.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.3",
    "Footer 3",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 42,
    SchemaVersion = 1)]
public sealed class Footer3Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.3";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias.";
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
