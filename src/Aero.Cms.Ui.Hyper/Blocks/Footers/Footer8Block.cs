using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 8 — centered logo, description, navigation links, and social icons.
/// Source: hyperui/public/examples/marketing/footers/8.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.8",
    "Footer 8",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 47,
    SchemaVersion = 1)]
public sealed class Footer8Block : BlockBase
{
    public const string BlockTypeId = "hyper.footers.8";

    public override string BlockType => BlockTypeId;

    public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque.";
    public List<FooterLink> NavLinks { get; set; } = DefaultNavLinks.Select(CloneLink).ToList();
    public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();

    public static readonly List<FooterLink> DefaultNavLinks =
    [
        new() { Text = "About" },
        new() { Text = "Careers" },
        new() { Text = "History" },
        new() { Text = "Services" },
        new() { Text = "Projects" },
        new() { Text = "Blog" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
