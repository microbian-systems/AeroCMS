using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 5 — offset shadow button with hover translate effect.
/// Source: hyperui/public/examples/marketing/buttons/5.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.5",
    "Button 5",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 139,
    SchemaVersion = 1)]
public sealed class Button5Block : BlockBase
{
    public const string BlockTypeId = "hyper.buttons.5";

    public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Offset direction: "hover-out" (shadow visible, moves out on hover) or "hover-in" (hidden, moves in on hover).</summary>
    public string OffsetStyle { get; set; } = "hover-out";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
