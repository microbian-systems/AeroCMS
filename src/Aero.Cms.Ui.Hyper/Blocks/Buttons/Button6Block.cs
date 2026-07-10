using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 6 — slide-in arrow icon button on hover with solid/bordered and left/right variants.
/// Source: hyperui/public/examples/marketing/buttons/6.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.6",
    "Button 6",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 140,
    SchemaVersion = 1)]
public sealed class Button6Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.buttons.6";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style: "solid" (filled bg) or "bordered" (border-only).</summary>
    public string Style { get; set; } = "solid";

    /// <summary>Icon position: "start" (left side, slides in from left) or "end" (right side, slides in from right).</summary>
    public string IconPosition { get; set; } = "start";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
