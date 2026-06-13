using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("media.audio", "Audio", Category = "Media", Icon = "volume-2", SortOrder = 50, SchemaVersion = 1)]
public sealed class AudioBlock : BlockBase
{
    public override string BlockType => "media.audio";
    public string Src { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public bool Controls { get; set; } = true;
    public bool Autoplay { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
