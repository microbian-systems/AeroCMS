using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// HyperUI Cards 6 — dark profile card with avatar, social links, and projects list.
/// Source: hyperui/public/examples/marketing/cards/6.html.
/// </summary>
[BlockMetadata(
    "hyper.cards.6",
    "Card 6",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 99,
    SchemaVersion = 1)]
public sealed class Card6Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.cards.6";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "Claire Mac";
        /// <summary>
    /// Gets or sets the Avatar Url.
    /// </summary>
public string AvatarUrl { get; set; } = "https://images.unsplash.com/photo-1614644147724-2d4785d69962?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public List<Card6SocialLink> SocialLinks { get; set; } = DefaultSocialLinks.Select(CloneSocialLink).ToList();
        /// <summary>
    /// Gets or sets the Projects.
    /// </summary>
public List<Card6Project> Projects { get; set; } = DefaultProjects.Select(CloneProject).ToList();

        /// <summary>
    /// DefaultSocialLinks.
    /// </summary>
public static readonly List<Card6SocialLink> DefaultSocialLinks =
    [
        new() { Name = "Twitter", Url = "#" },
        new() { Name = "GitHub", Url = "#" },
        new() { Name = "Website", Url = "#" }
    ];

        /// <summary>
    /// DefaultProjects.
    /// </summary>
public static readonly List<Card6Project> DefaultProjects =
    [
        new() { Title = "Project A", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Maxime consequuntur deleniti, unde ab ut in!", Url = "#" },
        new() { Title = "Project B", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Sapiente cumque saepe sit.", Url = "#" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Card6SocialLink CloneSocialLink(Card6SocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };

    private static Card6Project CloneProject(Card6Project project) => new()
    {
        Title = project.Title,
        Description = project.Description,
        Url = project.Url
    };
}

/// <summary>
/// Represents a class for Card6SocialLink.
/// </summary>
public sealed class Card6SocialLink
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; set; } = "";
}

/// <summary>
/// Represents a class for Card6Project.
/// </summary>
public sealed class Card6Project
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "";
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; set; } = "";
}
