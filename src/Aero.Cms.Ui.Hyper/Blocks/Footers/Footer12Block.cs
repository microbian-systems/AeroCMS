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
    public const string BlockTypeId = "hyper.footers.12";

    public override string BlockType => BlockTypeId;

    public string CtaTitle { get; set; } = "Make Your Next Career Move!";
    public string CtaText { get; set; } = "Let's Get Started";
    public string CtaUrl { get; set; } = "#";
    public string Description { get; set; } = "CTA banner footer with link columns and social icons.";
    public List<FooterLinkColumn> LinkColumns { get; set; } = DefaultLinkColumns.Select(CloneColumn).ToList();
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
    public string Copyright { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

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

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLinkColumn CloneColumn(FooterLinkColumn col) => new()
    {
        Title = col.Title,
        Links = col.Links.Select(l => new FooterLink { Text = l.Text, Url = l.Url }).ToList()
    };
}
