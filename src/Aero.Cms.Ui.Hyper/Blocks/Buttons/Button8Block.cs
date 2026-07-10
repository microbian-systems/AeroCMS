using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 8 — rotate on hover button with solid/bordered and positive/negative rotation variants.
/// Source: hyperui/public/examples/marketing/buttons/8.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.8",
    "Button 8",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 142,
    SchemaVersion = 1)]
public sealed class Button8Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.buttons.8";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style variant: "solid" (filled bg) or "bordered" (border-only).</summary>
    public string Style { get; set; } = "solid";

    /// <summary>Rotate direction: "positive" (rotate-2) or "negative" (-rotate-2).</summary>
    public string RotateDirection { get; set; } = "positive";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
