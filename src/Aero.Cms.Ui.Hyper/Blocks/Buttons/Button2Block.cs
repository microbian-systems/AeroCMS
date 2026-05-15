using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 2 — text with right arrow icon, solid and bordered variants.
/// Source: hyperui/public/examples/marketing/buttons/2.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.2",
    "Button 2",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 136,
    SchemaVersion = 1)]
public sealed class Button2Block : BlockBase
{
    public const string BlockTypeId = "hyper.buttons.2";

    public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style variant: "solid" (filled bg) or "bordered" (border-only).</summary>
    public string Style { get; set; } = "solid";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
