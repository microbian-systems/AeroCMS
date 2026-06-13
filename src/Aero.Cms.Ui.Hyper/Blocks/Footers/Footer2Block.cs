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
    public const string BlockTypeId = "hyper.footers.2";

    public override string BlockType => BlockTypeId;

    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
    public List<FooterLinkColumn> LinkColumns { get; set; } = FooterDefaults.DefaultLinkColumns4.Select(FooterDefaults.CloneColumn).ToList();
    public string Copyright { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
