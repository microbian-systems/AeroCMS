using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// HyperUI Header 3 — top navigation bar with logo, nav links, and login/register buttons.
/// Source: hyperui/public/examples/marketing/headers/3.html.
/// </summary>
[BlockMetadata(
    "hyper.headers.3",
    "Header 3",
    Category = "Hyper",
    Icon = "panel-top",
    SortOrder = 32,
    SchemaVersion = 1)]
public sealed class Header3Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.headers.3";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Nav Links.
    /// </summary>
public List<HyperNavLink> NavLinks { get; set; } = DefaultNavLinks.Select(CloneNavLink).ToList();
        /// <summary>
    /// Gets or sets the Login Url.
    /// </summary>
public string LoginUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Register Url.
    /// </summary>
public string RegisterUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Login Text.
    /// </summary>
public string LoginText { get; set; } = "Login";
        /// <summary>
    /// Gets or sets the Register Text.
    /// </summary>
public string RegisterText { get; set; } = "Register";

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
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };
}
