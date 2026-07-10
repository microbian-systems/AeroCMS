using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for VideoBlock.
/// </summary>
[BlockMetadata("media.video", "Video", Category = "Media", Icon = "video", SortOrder = 40, SchemaVersion = 1)]
public sealed class VideoBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "media.video";
        /// <summary>
    /// Gets or sets the Src.
    /// </summary>
public string Src { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Poster.
    /// </summary>
public string? Poster { get; set; }
        /// <summary>
    /// Gets or sets the Caption.
    /// </summary>
public string? Caption { get; set; }
        /// <summary>
    /// Gets or sets the Autoplay.
    /// </summary>
public bool Autoplay { get; set; }
        /// <summary>
    /// Gets or sets the Loop.
    /// </summary>
public bool Loop { get; set; }
        /// <summary>
    /// Gets or sets the Controls.
    /// </summary>
public bool Controls { get; set; } = true;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
