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
    public const string BlockTypeId = "hyper.cards.6";

    public override string BlockType => BlockTypeId;

    public string Name { get; set; } = "Claire Mac";
    public string AvatarUrl { get; set; } = "https://images.unsplash.com/photo-1614644147724-2d4785d69962?auto=format&fit=crop&q=80&w=1160";
    public List<Card6SocialLink> SocialLinks { get; set; } = DefaultSocialLinks.Select(CloneSocialLink).ToList();
    public List<Card6Project> Projects { get; set; } = DefaultProjects.Select(CloneProject).ToList();

    public static readonly List<Card6SocialLink> DefaultSocialLinks =
    [
        new() { Name = "Twitter", Url = "#" },
        new() { Name = "GitHub", Url = "#" },
        new() { Name = "Website", Url = "#" }
    ];

    public static readonly List<Card6Project> DefaultProjects =
    [
        new() { Title = "Project A", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Maxime consequuntur deleniti, unde ab ut in!", Url = "#" },
        new() { Title = "Project B", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Sapiente cumque saepe sit.", Url = "#" }
    ];

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

public sealed class Card6SocialLink
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class Card6Project
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
}
