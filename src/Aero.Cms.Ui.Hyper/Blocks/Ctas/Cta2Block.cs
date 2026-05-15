using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// HyperUI CTA 2 — centered text with email signup form (rose button).
/// Source: hyperui/public/examples/marketing/ctas/2.html, 2-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.ctas.2",
    "CTA 2",
    Category = "Hyper",
    Icon = "megaphone",
    SortOrder = 67,
    SchemaVersion = 1)]
public sealed class Cta2Block : BlockBase
{
    public const string BlockTypeId = "hyper.ctas.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Quae dolor officia blanditiis repellat in, vero, aperiam porro ipsum laboriosam consequuntur exercitationem incidunt tempora nisi?";
    public string ButtonText { get; set; } = "Sign Up";
    public string Placeholder { get; set; } = "Email address";
    public string FormAction { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
