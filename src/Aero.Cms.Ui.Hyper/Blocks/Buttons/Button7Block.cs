using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 7 — scale on hover button with solid and bordered variants.
/// Source: hyperui/public/examples/marketing/buttons/7.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.7",
    "Button 7",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 141,
    SchemaVersion = 1)]
public sealed class Button7Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.buttons.7";

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

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
