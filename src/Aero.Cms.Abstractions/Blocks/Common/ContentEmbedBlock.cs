using Aero.Cms.Abstractions.Content;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

[BlockMetadata("content_embed", "Content Embed", Category = "Content")]
public sealed class ContentEmbedBlock : BlockBase
{
    public const string Discriminator = "content_embed";
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

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
