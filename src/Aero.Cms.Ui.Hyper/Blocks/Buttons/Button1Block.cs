using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// HyperUI Button 1 — solid and bordered style buttons with hover swap.
/// Source: hyperui/public/examples/marketing/buttons/1.html.
/// </summary>
[BlockMetadata(
    "hyper.buttons.1",
    "Button 1",
    Category = "Hyper",
    Icon = "square",
    SortOrder = 135,
    SchemaVersion = 1)]
public sealed class Button1Block : BlockBase
{
    public const string BlockTypeId = "hyper.buttons.1";

    public override string BlockType => BlockTypeId;

    /// <summary>Button label text.</summary>
    public string Text { get; set; } = "Download";

    /// <summary>Optional link URL.</summary>
    public string Url { get; set; } = "#";

    /// <summary>Style variant: "solid" (filled bg, hover→transparent) or "bordered" (border-only, hover→filled).</summary>
    public string Style { get; set; } = "solid";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
