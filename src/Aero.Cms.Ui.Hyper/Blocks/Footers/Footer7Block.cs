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
    public const string BlockTypeId = "hyper.footers.7";

    public override string BlockType => BlockTypeId;

    public string NewsletterTitle { get; set; } = "Want us to email you with the latest blockbuster news?";
    public string NewsletterDescription { get; set; } = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Praesentium natus quod eveniet aut perferendis distinctio iusto repudiandae, provident velit earum?";
    public string EmailPlaceholder { get; set; } = "john@doe.com";
    public string ButtonText { get; set; } = "Subscribe";
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();
    public List<FooterLink> ServicesLinks { get; set; } = DefaultServicesLinks.Select(CloneLink).ToList();
    public List<FooterLink> AboutLinks { get; set; } = DefaultAboutLinks.Select(CloneLink).ToList();
    public List<FooterLink> SupportLinks { get; set; } = DefaultSupportLinks.Select(CloneLink).ToList();
    public string CopyrightText { get; set; } = "&copy; Company 2022. All rights reserved.";
    public string CreatedWithText { get; set; } = "Created with Laravel and Laravel Livewire.";

    public static readonly List<FooterLink> DefaultServicesLinks =
    [
        new() { Text = "Marketing" },
        new() { Text = "Graphic Design" },
        new() { Text = "App Development" },
        new() { Text = "Web Development" }
    ];

    public static readonly List<FooterLink> DefaultAboutLinks =
    [
        new() { Text = "About" },
        new() { Text = "Careers" },
        new() { Text = "History" },
        new() { Text = "Our Team" }
    ];

    public static readonly List<FooterLink> DefaultSupportLinks =
    [
        new() { Text = "FAQs" },
        new() { Text = "Contact" },
        new() { Text = "Live Chat" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
