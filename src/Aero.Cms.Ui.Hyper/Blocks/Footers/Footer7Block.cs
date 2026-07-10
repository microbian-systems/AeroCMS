using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 7 — newsletter signup with description, social links, link columns, and copyright.
/// Source: hyperui/public/examples/marketing/footers/7.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.7",
    "Footer 7",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 46,
    SchemaVersion = 1)]
public sealed class Footer7Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.7";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Newsletter Title.
    /// </summary>
public string NewsletterTitle { get; set; } = "Want us to email you with the latest blockbuster news?";
        /// <summary>
    /// Gets or sets the Newsletter Description.
    /// </summary>
public string NewsletterDescription { get; set; } = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Praesentium natus quod eveniet aut perferendis distinctio iusto repudiandae, provident velit earum?";
        /// <summary>
    /// Gets or sets the Email Placeholder.
    /// </summary>
public string EmailPlaceholder { get; set; } = "john@doe.com";
        /// <summary>
    /// Gets or sets the Button Text.
    /// </summary>
public string ButtonText { get; set; } = "Subscribe";
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
        /// <summary>
    /// Gets or sets the Services Links.
    /// </summary>
public List<FooterLink> ServicesLinks { get; set; } = DefaultServicesLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the About Links.
    /// </summary>
public List<FooterLink> AboutLinks { get; set; } = DefaultAboutLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Support Links.
    /// </summary>
public List<FooterLink> SupportLinks { get; set; } = DefaultSupportLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Copyright Text.
    /// </summary>
public string CopyrightText { get; set; } = "&copy; Company 2022. All rights reserved.";
        /// <summary>
    /// Gets or sets the Created With Text.
    /// </summary>
public string CreatedWithText { get; set; } = "Created with Laravel and Laravel Livewire.";

        /// <summary>
    /// DefaultServicesLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultServicesLinks =
    [
        new() { Text = "Marketing" },
        new() { Text = "Graphic Design" },
        new() { Text = "App Development" },
        new() { Text = "Web Development" }
    ];

        /// <summary>
    /// DefaultAboutLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultAboutLinks =
    [
        new() { Text = "About" },
        new() { Text = "Careers" },
        new() { Text = "History" },
        new() { Text = "Our Team" }
    ];

        /// <summary>
    /// DefaultSupportLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultSupportLinks =
    [
        new() { Text = "FAQs" },
        new() { Text = "Contact" },
        new() { Text = "Live Chat" }
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
