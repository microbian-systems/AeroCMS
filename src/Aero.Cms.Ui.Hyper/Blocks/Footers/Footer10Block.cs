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
    public const string BlockTypeId = "hyper.footers.10";

    public override string BlockType => BlockTypeId;

    public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque.";
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
    public List<FooterLinkColumn> LinkColumns { get; set; } = DefaultLinkColumns.Select(CloneColumn).ToList();
    public string Copyright { get; set; } = "&copy; 2022 Company Name";
    public List<FooterLink> LegalLinks { get; set; } = DefaultLegalLinks.Select(CloneLink).ToList();

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

    public static readonly List<FooterLink> DefaultLegalLinks =
    [
        new() { Text = "Terms & Conditions" },
        new() { Text = "Privacy Policy" }
    ];

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
