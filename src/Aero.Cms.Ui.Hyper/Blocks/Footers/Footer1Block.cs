using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 1 — newsletter signup with link columns and social icons.
/// Source: hyperui/public/examples/marketing/footers/1.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.1",
    "Footer 1",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 40,
    SchemaVersion = 1)]
public sealed class Footer1Block : BlockBase
{
    public const string BlockTypeId = "hyper.footers.1";

    public override string BlockType => BlockTypeId;

    public string NewsletterTitle { get; set; } = "Get the latest news!";
    public string NewsletterDescription { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias.";
    public string EmailPlaceholder { get; set; } = "john@rhcp.com";
    public string ButtonText { get; set; } = "Sign Up";
    public List<FooterLinkColumn> LinkColumns { get; set; } = DefaultLinkColumns.Select(CloneColumn).ToList();
    public List<FooterSocialLink> SocialLinks { get; set; } = DefaultSocialLinks.Select(CloneSocialLink).ToList();
    public string Copyright { get; set; } = "&copy; 2022. Company Name. All rights reserved.";
    public List<FooterLink> BottomLinks { get; set; } = DefaultBottomLinks.Select(CloneLink).ToList();

    public static readonly List<FooterLinkColumn> DefaultLinkColumns =
    [
        new()
        {
            Title = "Services",
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
            Title = "Company",
            Links =
            [
                new() { Text = "About" },
                new() { Text = "Meet the Team" },
                new() { Text = "Accounts Review" }
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
        },
        new()
        {
            Title = "Legal",
            Links =
            [
                new() { Text = "Accessibility" },
                new() { Text = "Returns Policy" },
                new() { Text = "Refund Policy" },
                new() { Text = "Hiring-3 Statistics" }
            ]
        },
        new()
        {
            Title = "Downloads",
            Links =
            [
                new() { Text = "Marketing Calendar" },
                new() { Text = "SEO Infographics" }
            ]
        }
    ];

    public static readonly List<FooterSocialLink> DefaultSocialLinks =
    [
        new() { Name = "Facebook" },
        new() { Name = "Instagram" },
        new() { Name = "Twitter" },
        new() { Name = "GitHub" },
        new() { Name = "Dribbble" }
    ];

    public static readonly List<FooterLink> DefaultBottomLinks =
    [
        new() { Text = "Terms & Conditions" },
        new() { Text = "Privacy Policy" },
        new() { Text = "Cookies" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLinkColumn CloneColumn(FooterLinkColumn col) => new()
    {
        Title = col.Title,
        Links = col.Links.Select(l => new FooterLink { Text = l.Text, Url = l.Url }).ToList()
    };

    private static FooterSocialLink CloneSocialLink(FooterSocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}

public sealed class FooterLinkColumn
{
    public string Title { get; set; } = "";
    public List<FooterLink> Links { get; set; } = [];
}

public sealed class FooterLink
{
    public string Text { get; set; } = "";
    public string Url { get; set; } = "#";
}

public sealed class FooterSocialLink
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "#";
}
