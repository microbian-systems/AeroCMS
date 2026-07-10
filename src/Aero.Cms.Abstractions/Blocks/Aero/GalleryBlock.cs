using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for GalleryBlock.
/// </summary>
[BlockMetadata("media.gallery", "Gallery", Category = "Media", Icon = "layout-grid", SortOrder = 60, SchemaVersion = 1)]
public sealed class GalleryBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "media.gallery";
        /// <summary>
    /// Gets or sets the Images.
    /// </summary>
public List<string> Images { get; set; } = [];
        /// <summary>
    /// Gets or sets the Columns.
    /// </summary>
public int Columns { get; set; } = 3;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
