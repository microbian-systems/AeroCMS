using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("media.image", "Image", Category = "Media", Icon = "image", SortOrder = 30, SchemaVersion = 1)]
public sealed class ImageBlock : BlockBase
{
    public override string BlockType => "media.image";
    public string Src { get; set; } = "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?w=800";
    public string Alt { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public long ImageMediaId { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
