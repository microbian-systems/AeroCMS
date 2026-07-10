using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 10 — reveal underline/side bar on hover (left, right, bottom, top).
/// Source: hyperui/public/examples/marketing/buttons/10.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.10",
    "Button 10",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 144,
    SchemaVersion = 1)]
public sealed class Button10Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.buttons.10";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Reveal direction: "left", "right", "bottom", or "top".</summary>
    public string RevealDirection { get; set; } = "left";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
