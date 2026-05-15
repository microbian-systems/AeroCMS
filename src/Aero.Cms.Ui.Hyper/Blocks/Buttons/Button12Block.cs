using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 12 — offset lift button with border shadow, solid red and bordered variants.
/// Source: hyperui/public/examples/marketing/buttons/12.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.12",
    "Button 12",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 146,
    SchemaVersion = 1)]
public sealed class Button12Block : BlockBase
{
    public const string BlockTypeId = "hyper.buttons.12";

    public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style variant: "solid" (filled red bg with white text) or "bordered" (border-only with red text).</summary>
    public string Style { get; set; } = "solid";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
