using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 4 — icon-only circular button with arrow, solid and bordered.
/// Source: hyperui/public/examples/marketing/buttons/4.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.4",
    "Button 4",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 138,
    SchemaVersion = 1)]
public sealed class Button4Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.buttons.4";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

    /// <summary>Accessible label (sr-only text).</summary>
    public string Label { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style variant: "solid" (filled bg) or "bordered" (border-only).</summary>
    public string Style { get; set; } = "solid";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
