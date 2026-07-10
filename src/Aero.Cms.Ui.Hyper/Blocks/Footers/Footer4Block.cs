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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.4";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Customise Your Product";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Cum maiores ipsum eos temporibus ea nihil.";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Get Started";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Bottom Links.
    /// </summary>
public List<FooterLink> BottomLinks { get; set; } = DefaultBottomLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();

        /// <summary>
    /// DefaultBottomLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultBottomLinks =
    [
        new() { Text = "Terms & Conditions" },
        new() { Text = "Privacy Policy" },
        new() { Text = "Cookies" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
