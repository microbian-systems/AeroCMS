using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 6 — two-column layout with demo request CTA and link columns.
/// Source: hyperui/public/examples/marketing/footers/6.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.6",
    "Footer 6",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 45,
    SchemaVersion = 1)]
public sealed class Footer6Block : BlockBase
{
    public const string BlockTypeId = "hyper.footers.6";

    public override string BlockType => BlockTypeId;

    public string CtaTitle { get; set; } = "Request a Demo";
    public string CtaDescription { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Veritatis, harum deserunt nesciunt praesentium, repellendus eum perspiciatis ratione pariatur a aperiam eius numquam doloribus asperiores sunt.";
    public string EmailPlaceholder { get; set; } = "john@rhcp.com";
    public string ButtonText { get; set; } = "Sign Up";
    public List<FooterLink> ServicesLinks { get; set; } = DefaultServicesLinks.Select(CloneLink).ToList();
    public List<FooterLink> CompanyLinks { get; set; } = DefaultCompanyLinks.Select(CloneLink).ToList();
    public List<FooterLink> HelpfulLinks { get; set; } = DefaultHelpfulLinks.Select(CloneLink).ToList();
    public List<FooterLink> BottomLinks { get; set; } = DefaultBottomLinks.Select(CloneLink).ToList();
    public string CopyrightText { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

    public static readonly List<FooterLink> DefaultServicesLinks =
    [
        new() { Text = "1on1 Coaching" },
        new() { Text = "Company Review" },
        new() { Text = "Accounts Review" },
        new() { Text = "HR Consulting" },
        new() { Text = "SEO Optimisation" }
    ];

    public static readonly List<FooterLink> DefaultCompanyLinks =
    [
        new() { Text = "About" },
        new() { Text = "Meet the Team" },
        new() { Text = "Accounts Review" }
    ];

    public static readonly List<FooterLink> DefaultHelpfulLinks =
    [
        new() { Text = "Contact" },
        new() { Text = "FAQs" },
        new() { Text = "Live Chat" }
    ];

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
