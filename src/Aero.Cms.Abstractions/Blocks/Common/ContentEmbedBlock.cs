using Aero.Cms.Abstractions.Content;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// Represents a class for ContentEmbedBlock.
/// </summary>
[BlockMetadata("content_embed", "Content Embed", Category = "Content")]
public sealed class ContentEmbedBlock : BlockBase
{
        /// <summary>
    /// Discriminator.
    /// </summary>
public const string Discriminator = "content_embed";
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => Discriminator;

    /// <summary>The ContentItem to render.</summary>
    public long ContentItemId { get; set; }

    /// <summary>
    /// Which rendering path to use: DynamicBlock (Scriban) or
    /// BlockLayout (individual block instances per field).
    /// </summary>
    public ContentTypeRenderMode RenderMode { get; set; } = ContentTypeRenderMode.DynamicBlock;

    /// <summary>
    /// Optional per-field override mappings.
    /// </summary>
    public List<ContentEmbedFieldMapping>? FieldOverrides { get; set; }

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
