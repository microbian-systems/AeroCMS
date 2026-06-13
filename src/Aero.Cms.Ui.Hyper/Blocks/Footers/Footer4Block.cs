using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 4 — centered CTA section with bottom legal links and social icons.
/// Source: hyperui/public/examples/marketing/footers/4.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.4",
    "Footer 4",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 43,
    SchemaVersion = 1)]
public sealed class Footer4Block : BlockBase
{
    public const string BlockTypeId = "hyper.footers.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Customise Your Product";
    public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Cum maiores ipsum eos temporibus ea nihil.";
    public string CtaText { get; set; } = "Get Started";
    public string CtaUrl { get; set; } = "#";
    public List<FooterLink> BottomLinks { get; set; } = DefaultBottomLinks.Select(CloneLink).ToList();
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();

    public static readonly List<FooterLink> DefaultBottomLinks =
    [
        new() { Text = "Terms & Conditions" },
        new() { Text = "Privacy Policy" },
        new() { Text = "Cookies" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
