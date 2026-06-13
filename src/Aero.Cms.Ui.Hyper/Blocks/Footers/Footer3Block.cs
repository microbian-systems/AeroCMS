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
    public const string BlockTypeId = "hyper.footers.3";

    public override string BlockType => BlockTypeId;

    public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias.";
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
    public List<FooterLinkColumn> LinkColumns { get; set; } = FooterDefaults.DefaultLinkColumns4.Select(FooterDefaults.CloneColumn).ToList();
    public string Copyright { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
