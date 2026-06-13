using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("neo.layout.columns", "Columns", Category = "Layout", Icon = "columns-2", SortOrder = 90, SchemaVersion = 1)]
public sealed class NeoColumnsBlock : BlockBase
{
    public override string BlockType => "neo.layout.columns";
    public List<ColumnItem> Items { get; set; } = [new(), new()];
    public int Gap { get; set; } = 4;
    public bool EqualHeight { get; set; } = true;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public sealed class ColumnItem
{
    public string Content { get; set; } = string.Empty;
    public int Span { get; set; } = 6;
}
