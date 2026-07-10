using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 12 — CTA banner with link columns, social icons, and logo.
/// Source: hyperui/public/examples/marketing/footers/12.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.12",
    "Footer 12",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 51,
    SchemaVersion = 1)]
public sealed class Footer12Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.12";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Cta Title.
    /// </summary>
public string CtaTitle { get; set; } = "Make Your Next Career Move!";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Let's Get Started";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "CTA banner footer with link columns and social icons.";
        /// <summary>
    /// Gets or sets the Link Columns.
    /// </summary>
public List<FooterLinkColumn> LinkColumns { get; set; } = DefaultLinkColumns.Select(CloneColumn).ToList();
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
        /// <summary>
    /// Gets or sets the Copyright.
    /// </summary>
public string Copyright { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

        /// <summary>
    /// DefaultLinkColumns.
    /// </summary>
public static readonly List<FooterLinkColumn> DefaultLinkColumns =
    [
        new()
        {
            Title = "About Us",
            Links =
            [
                new() { Text = "About" },
                new() { Text = "Meet the Team" },
                new() { Text = "Accounts Review" },
                new() { Text = "HR Consulting" }
            ]
        },
        new()
        {
            Title = "Our Services",
            Links =
            [
                new() { Text = "1on1 Coaching" },
                new() { Text = "Company Review" },
                new() { Text = "Accounts Review" },
                new() { Text = "HR Consulting" },
                new() { Text = "SEO Optimisation" }
            ]
        },
        new()
        {
            Title = "Resources",
            Links =
            [
                new() { Text = "Blog" },
                new() { Text = "Case Studies" },
                new() { Text = "Whitepapers" },
                new() { Text = "Webinars" }
            ]
        },
        new()
        {
            Title = "Helpful Links",
            Links =
            [
                new() { Text = "Contact" },
                new() { Text = "FAQs" },
                new() { Text = "Live Chat" }
            ]
        }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLinkColumn CloneColumn(FooterLinkColumn col) => new()
    {
        Title = col.Title,
        Links = col.Links.Select(l => new FooterLink { Text = l.Text, Url = l.Url }).ToList()
    };
}
