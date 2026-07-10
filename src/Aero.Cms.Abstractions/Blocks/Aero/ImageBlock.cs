using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for ImageBlock.
/// </summary>
[BlockMetadata("media.image", "Image", Category = "Media", Icon = "image", SortOrder = 30, SchemaVersion = 1)]
public sealed class ImageBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "media.image";
        /// <summary>
    /// Gets or sets the Src.
    /// </summary>
public string Src { get; set; } = "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?w=800";
        /// <summary>
    /// Gets or sets the Alt.
    /// </summary>
public string Alt { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Caption.
    /// </summary>
public string? Caption { get; set; }
        /// <summary>
    /// Gets or sets the Image Media Id.
    /// </summary>
public long ImageMediaId { get; set; }

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
