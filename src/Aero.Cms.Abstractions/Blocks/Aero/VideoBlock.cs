using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("media.video", "Video", Category = "Media", Icon = "video", SortOrder = 40, SchemaVersion = 1)]
public sealed class VideoBlock : BlockBase
{
    public override string BlockType => "media.video";
    public string Src { get; set; } = string.Empty;
    public string? Poster { get; set; }
    public string? Caption { get; set; }
    public bool Autoplay { get; set; }
    public bool Loop { get; set; }
    public bool Controls { get; set; } = true;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
