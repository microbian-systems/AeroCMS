using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 9 — bold uppercase offset shadow button with hover translate effect.
/// Source: hyperui/public/examples/marketing/buttons/9.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.9",
    "Button 9",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 143,
    SchemaVersion = 1)]
public sealed class Button9Block : BlockBase
{
    public const string BlockTypeId = "hyper.buttons.9";

    public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Offset direction: "hover-out" (shadow visible, moves out on hover) or "hover-in" (hidden, moves in on hover).</summary>
    public string OffsetStyle { get; set; } = "hover-out";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
