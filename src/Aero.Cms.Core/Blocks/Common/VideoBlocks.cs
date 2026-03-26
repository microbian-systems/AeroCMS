using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A specialized embed block for YouTube videos.
/// </summary>
[BlockMetadata("youtube_player", "YouTube Video", Category = "Video")]
public sealed class YouTubeBlock : EmbedBlock
{
    public override string BlockType => "youtube_player";
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A specialized embed block for Vimeo videos.
/// </summary>
[BlockMetadata("vimeo_player", "Vimeo Video", Category = "Video")]
public sealed class VimeoBlock : EmbedBlock
{
    public override string BlockType => "vimeo_player";
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A specialized embed block for Twitch videos or clips.
/// </summary>
[BlockMetadata("twitch_player", "Twitch Video/Clip", Category = "Video")]
public sealed class TwitchBlock : EmbedBlock
{
    public override string BlockType => "twitch_player";
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A specialized embed block for TikTok videos.
/// </summary>
[BlockMetadata("tiktok_player", "TikTok Video", Category = "Video")]
public sealed class TikTokBlock : EmbedBlock
{
    public override string BlockType => "tiktok_player";
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
