using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for AudioBlock.
/// </summary>
[BlockMetadata("media.audio", "Audio", Category = "Media", Icon = "volume-2", SortOrder = 50, SchemaVersion = 1)]
public sealed class AudioBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "media.audio";
        /// <summary>
    /// Gets or sets the Src.
    /// </summary>
public string Src { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Caption.
    /// </summary>
public string? Caption { get; set; }
        /// <summary>
    /// Gets or sets the Controls.
    /// </summary>
public bool Controls { get; set; } = true;
        /// <summary>
    /// Gets or sets the Autoplay.
    /// </summary>
public bool Autoplay { get; set; }

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
