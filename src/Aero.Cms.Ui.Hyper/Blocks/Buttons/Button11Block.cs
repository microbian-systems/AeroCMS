using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 11 — button with icon chip on the right, solid and bordered variants.
/// Source: hyperui/public/examples/marketing/buttons/11.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.11",
    "Button 11",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 145,
    SchemaVersion = 1)]
public sealed class Button11Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.buttons.11";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Find out more";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style variant: "solid" (filled bg) or "bordered" (border-only).</summary>
    public string Style { get; set; } = "solid";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
