using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// HyperUI Header 4 — navigation bar with logo, nav links, user avatar dropdown, and logout.
/// Source: hyperui/public/examples/marketing/headers/4.html.
/// </summary>
[BlockMetadata(
    "hyper.headers.4",
    "Header 4",
    Category = "Hyper",
    Icon = "panel-top",
    SortOrder = 33,
    SchemaVersion = 1)]
public sealed class Header4Block : BlockBase
{
    public const string BlockTypeId = "hyper.headers.4";

    public override string BlockType => BlockTypeId;

    public List<HyperNavLink> NavLinks { get; set; } = DefaultNavLinks.Select(CloneNavLink).ToList();
    public string? UserAvatarUrl { get; set; }
    public List<HyperNavLink> UserMenuItems { get; set; } = DefaultUserMenuItems.Select(CloneNavLink).ToList();
    public string? LogoutUrl { get; set; }
    public string LogoutText { get; set; } = "Logout";

    public static readonly List<HyperNavLink> DefaultNavLinks =
    [
        new() { Label = "About", Url = "#" },
        new() { Label = "Careers", Url = "#" },
        new() { Label = "History", Url = "#" },
        new() { Label = "Services", Url = "#" },
        new() { Label = "Projects", Url = "#" },
        new() { Label = "Blog", Url = "#" }
    ];

    public static readonly List<HyperNavLink> DefaultUserMenuItems =
    [
        new() { Label = "My profile", Url = "#" },
        new() { Label = "My data", Url = "#" },
        new() { Label = "Team settings", Url = "#" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };
}
