using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("media.gallery", "Gallery", Category = "Media", Icon = "layout-grid", SortOrder = 60, SchemaVersion = 1)]
public sealed class GalleryBlock : BlockBase
{
    public override string BlockType => "media.gallery";
    public List<string> Images { get; set; } = [];
    public int Columns { get; set; } = 3;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
