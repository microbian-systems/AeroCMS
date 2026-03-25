using System.Text.Json.Serialization;
using Aero.Core.Entities;
using Microsoft.AspNetCore.Html;

using Aero.Cms.Core.Blocks.Common;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Represents the base class for all CMS blocks with AOT-compatible polymorphic serialization.
/// </summary>
/// <remarks>
/// Concrete block types must be decorated with <see cref="JsonDerivedTypeAttribute"/> 
/// specifying the type discriminator for proper serialization.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$blockType")]
[JsonDerivedType(typeof(RichTextBlock), "rich_text")]
[JsonDerivedType(typeof(HeadingBlock), "heading")]
[JsonDerivedType(typeof(ImageBlock), "image")]
[JsonDerivedType(typeof(CtaBlock), "cta")]
[JsonDerivedType(typeof(QuoteBlock), "quote")]
[JsonDerivedType(typeof(EmbedBlock), "embed")]
[JsonDerivedType(typeof(YouTubeBlock), "youtube_player")]
[JsonDerivedType(typeof(VimeoBlock), "vimeo_player")]
[JsonDerivedType(typeof(TwitchBlock), "twitch_player")]
[JsonDerivedType(typeof(TikTokBlock), "tiktok_player")]
[JsonDerivedType(typeof(ColumnsBlock), "columns")]
[JsonDerivedType(typeof(CardBlock), "cards")]
[JsonDerivedType(typeof(CarouselBlock), "carousel")]
[JsonDerivedType(typeof(ContentLinkBlock), "content_link")]
[JsonDerivedType(typeof(HeroBlock), "hero")]
[JsonDerivedType(typeof(NavigationBlock), "navigation")]
public abstract class BlockBase : Entity, IBlock
{
    /// <summary>
    /// Gets the type discriminator of the block.
    /// </summary>
    [JsonPropertyName("blockType")]
    public abstract string BlockType { get; }

    /// <summary>
    /// Gets the display order of the block within its parent content.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Accepts a visitor for rendering the block.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The HTML content rendered by the visitor.</returns>
    public abstract IHtmlContent Accept(IBlockVisitor visitor);
}
