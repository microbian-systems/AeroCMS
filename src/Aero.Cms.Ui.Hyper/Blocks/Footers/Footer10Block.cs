using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 10 — footer with logo, description, social icons, four link columns, and legal links.
/// Source: hyperui/public/examples/marketing/footers/10.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.10",
    "Footer 10",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 49,
    SchemaVersion = 1)]
public sealed class Footer10Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.10";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque.";
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
        /// <summary>
    /// Gets or sets the Link Columns.
    /// </summary>
public List<FooterLinkColumn> LinkColumns { get; set; } = DefaultLinkColumns.Select(CloneColumn).ToList();
        /// <summary>
    /// Gets or sets the Copyright.
    /// </summary>
public string Copyright { get; set; } = "&copy; 2022 Company Name";
        /// <summary>
    /// Gets or sets the Legal Links.
    /// </summary>
public List<FooterLink> LegalLinks { get; set; } = DefaultLegalLinks.Select(CloneLink).ToList();

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
                new() { Text = "Company History" },
                new() { Text = "Meet the Team" },
                new() { Text = "Employee Handbook" },
                new() { Text = "Careers" }
            ]
        },
        new()
        {
            Title = "Our Services",
            Links =
            [
                new() { Text = "Web Development" },
                new() { Text = "Web Design" },
                new() { Text = "Marketing" },
                new() { Text = "Google Ads" }
            ]
        },
        new()
        {
            Title = "Helpful Links",
            Links =
            [
                new() { Text = "FAQs" },
                new() { Text = "Support" },
                new() { Text = "Live Chat" }
            ]
        },
        new()
        {
            Title = "Contact Us",
            Links =
            [
                new() { Text = "john@doe.com" },
                new() { Text = "0123456789" },
                new() { Text = "213 Lane, London, United Kingdom" }
            ]
        }
    ];

        /// <summary>
    /// DefaultLegalLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultLegalLinks =
    [
        new() { Text = "Terms & Conditions" },
        new() { Text = "Privacy Policy" }
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

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
