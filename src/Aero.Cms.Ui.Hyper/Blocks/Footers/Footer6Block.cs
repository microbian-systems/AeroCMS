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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.6";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Cta Title.
    /// </summary>
public string CtaTitle { get; set; } = "Request a Demo";
        /// <summary>
    /// Gets or sets the Cta Description.
    /// </summary>
public string CtaDescription { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Veritatis, harum deserunt nesciunt praesentium, repellendus eum perspiciatis ratione pariatur a aperiam eius numquam doloribus asperiores sunt.";
        /// <summary>
    /// Gets or sets the Email Placeholder.
    /// </summary>
public string EmailPlaceholder { get; set; } = "john@rhcp.com";
        /// <summary>
    /// Gets or sets the Button Text.
    /// </summary>
public string ButtonText { get; set; } = "Sign Up";
        /// <summary>
    /// Gets or sets the Services Links.
    /// </summary>
public List<FooterLink> ServicesLinks { get; set; } = DefaultServicesLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Company Links.
    /// </summary>
public List<FooterLink> CompanyLinks { get; set; } = DefaultCompanyLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Helpful Links.
    /// </summary>
public List<FooterLink> HelpfulLinks { get; set; } = DefaultHelpfulLinks.Select(CloneLink).ToList();
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
    /// DefaultHelpfulLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultHelpfulLinks =
    [
        new() { Text = "Contact" },
        new() { Text = "FAQs" },
        new() { Text = "Live Chat" }
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
