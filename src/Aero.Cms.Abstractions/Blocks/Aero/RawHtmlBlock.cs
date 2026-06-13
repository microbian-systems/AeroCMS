using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("ui.raw-html", "Raw HTML", Category = "UI", Icon = "code", SortOrder = 70, SchemaVersion = 1)]
public sealed class NeoRawHtmlBlock : BlockBase
{
    public override string BlockType => "ui.raw-html";
    public string Html { get; set; } = string.Empty;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
