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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.8";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque.";
        /// <summary>
    /// Gets or sets the Nav Links.
    /// </summary>
public List<FooterLink> NavLinks { get; set; } = DefaultNavLinks.Select(CloneLink).ToList();
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<FooterSocialLink> SocialLinks { get; set; } = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList();

        /// <summary>
    /// DefaultNavLinks.
    /// </summary>
public static readonly List<FooterLink> DefaultNavLinks =
    [
        new() { Text = "About" },
        new() { Text = "Careers" },
        new() { Text = "History" },
        new() { Text = "Services" },
        new() { Text = "Projects" },
        new() { Text = "Blog" }
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
