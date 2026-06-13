using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("ui.separator", "Separator", Category = "UI", Icon = "minus", SortOrder = 80, SchemaVersion = 1)]
public sealed class SeparatorBlock : BlockBase
{
    public override string BlockType => "ui.separator";
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
