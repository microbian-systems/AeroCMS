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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.5";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1642370324100-324b21fab3a9?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Call Us Text.
    /// </summary>
public string CallUsText { get; set; } = "Call us";
        /// <summary>
    /// Gets or sets the Phone Number.
    /// </summary>
public string PhoneNumber { get; set; } = "0123456789";
        /// <summary>
    /// Gets or sets the Hours.
    /// </summary>
public List<string> Hours { get; set; } =
    [
        "Monday to Friday: 10am - 5pm",
        "Weekend: 10am - 3pm"
    ];
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
        /// <summary>
    /// Gets or sets the Services Links.
    /// </summary>
public List<FooterLink> ServicesLinks { get; set; } = DefaultServicesLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Company Links.
    /// </summary>
public List<FooterLink> CompanyLinks { get; set; } = DefaultCompanyLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Bottom Links.
    /// </summary>
public List<FooterLink> BottomLinks { get; set; } = DefaultBottomLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Copyright Text.
    /// </summary>
public string CopyrightText { get; set; } = "&copy; 2022. Company Name. All rights reserved.";

        /// <summary>
    /// DefaultServicesLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultServicesLinks =
    [
        new() { Text = "1on1 Coaching" },
        new() { Text = "Company Review" },
        new() { Text = "Accounts Review" },
        new() { Text = "HR Consulting" },
        new() { Text = "SEO Optimisation" }
    ];

        /// <summary>
    /// DefaultCompanyLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultCompanyLinks =
    [
        new() { Text = "About" },
        new() { Text = "Meet the Team" },
        new() { Text = "Accounts Review" }
    ];

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
