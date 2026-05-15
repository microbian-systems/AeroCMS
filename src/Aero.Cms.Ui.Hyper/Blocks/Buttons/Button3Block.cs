using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 3 — gradient border button with rectangular and pill variants.
/// Source: hyperui/public/examples/marketing/buttons/3.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.3",
    "Button 3",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 137,
    SchemaVersion = 1)]
public sealed class Button3Block : BlockBase
{
    public const string BlockTypeId = "hyper.buttons.3";

    public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Rounded style: "sm" (rounded-sm) or "full" (rounded-full pill).</summary>
    public string RoundedStyle { get; set; } = "sm";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
