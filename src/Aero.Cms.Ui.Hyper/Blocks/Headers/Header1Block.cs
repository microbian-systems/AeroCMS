using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// HyperUI Header 1 — top navigation bar with logo, nav links, and login/register buttons.
/// Source: hyperui/public/examples/marketing/headers/1.html.
/// </summary>
[BlockMetadata(
    "hyper.headers.1",
    "Header 1",
    Category = "Hyper",
    Icon = "panel-top",
    SortOrder = 30,
    SchemaVersion = 1)]
public sealed class Header1Block : BlockBase
{
    public const string BlockTypeId = "hyper.headers.1";

    public override string BlockType => BlockTypeId;

    public List<HyperNavLink> NavLinks { get; set; } = DefaultNavLinks.Select(CloneNavLink).ToList();
    public string LoginUrl { get; set; } = "#";
    public string RegisterUrl { get; set; } = "#";
    public string LoginText { get; set; } = "Login";
    public string RegisterText { get; set; } = "Register";

    public static readonly List<HyperNavLink> DefaultNavLinks =
    [
        new() { Label = "About", Url = "#" },
        new() { Label = "Careers", Url = "#" },
        new() { Label = "History", Url = "#" },
        new() { Label = "Services", Url = "#" },
        new() { Label = "Projects", Url = "#" },
        new() { Label = "Blog", Url = "#" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };
}

/// <summary>
/// Represents a single navigation link used by header blocks.
/// </summary>
public sealed class HyperNavLink
{
    public string Label { get; set; } = "";
    public string Url { get; set; } = "#";
}
