using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 5 — image with contact info, social links, link columns, and bottom legal links.
/// Source: hyperui/public/examples/marketing/footers/5.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.5",
    "Footer 5",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 44,
    SchemaVersion = 1)]
public sealed class Footer5Block : BlockBase
{
    public const string BlockTypeId = "hyper.footers.5";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1642370324100-324b21fab3a9?auto=format&fit=crop&q=80&w=1160";
    public string CallUsText { get; set; } = "Call us";
    public string PhoneNumber { get; set; } = "0123456789";
    public List<string> Hours { get; set; } =
    [
        "Monday to Friday: 10am - 5pm",
        "Weekend: 10am - 3pm"
    ];
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
    public List<FooterLink> ServicesLinks { get; set; } = DefaultServicesLinks.Select(CloneLink).ToList();
    public List<FooterLink> CompanyLinks { get; set; } = DefaultCompanyLinks.Select(CloneLink).ToList();
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
