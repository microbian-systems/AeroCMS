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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.headers.4";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Nav Links.
    /// </summary>
public List<HyperNavLink> NavLinks { get; set; } = DefaultNavLinks.Select(CloneNavLink).ToList();
        /// <summary>
    /// Gets or sets the User Avatar Url.
    /// </summary>
public string? UserAvatarUrl { get; set; }
        /// <summary>
    /// Gets or sets the User Menu Items.
    /// </summary>
public List<HyperNavLink> UserMenuItems { get; set; } = DefaultUserMenuItems.Select(CloneNavLink).ToList();
        /// <summary>
    /// Gets or sets the Logout Url.
    /// </summary>
public string? LogoutUrl { get; set; }
        /// <summary>
    /// Gets or sets the Logout Text.
    /// </summary>
public string LogoutText { get; set; } = "Logout";

        /// <summary>
    /// DefaultNavLinks.
    /// </summary>
public static readonly List<HyperNavLink> DefaultNavLinks =
    [
        new() { Label = "About", Url = "#" },
        new() { Label = "Careers", Url = "#" },
        new() { Label = "History", Url = "#" },
        new() { Label = "Services", Url = "#" },
        new() { Label = "Projects", Url = "#" },
        new() { Label = "Blog", Url = "#" }
    ];

        /// <summary>
    /// DefaultUserMenuItems.
    /// </summary>
public static readonly List<HyperNavLink> DefaultUserMenuItems =
    [
        new() { Label = "My profile", Url = "#" },
        new() { Label = "My data", Url = "#" },
        new() { Label = "Team settings", Url = "#" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };
}
