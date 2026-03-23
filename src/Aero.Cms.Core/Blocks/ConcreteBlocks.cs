using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Represents a rich text content block with HTML content.
/// </summary>
[BlockMetadata("rich_text", "Rich Text")]
public sealed class RichTextBlock : BlockBase
{
    /// <summary>
    /// Gets the HTML content of the rich text block.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string BlockType => "rich_text";

    /// <inheritdoc />
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit((RichTextBlock)this);
}

/// <summary>
/// Represents a heading block with configurable heading level.
/// </summary>
[BlockMetadata("heading", "Heading")]
public sealed class HeadingBlock : BlockBase
{
    /// <summary>
    /// Gets the heading level (1-6 corresponding to H1-H6).
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Gets the text content of the heading.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string BlockType => "heading";

    /// <inheritdoc />
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit((HeadingBlock)this);
}

/// <summary>
/// Represents an image block referencing a media asset.
/// </summary>
[BlockMetadata("image", "Image")]
public sealed class ImageBlock : BlockBase
{
    /// <summary>
    /// Gets the unique identifier of the media asset.
    /// </summary>
    public long MediaId { get; set; }

    /// <summary>
    /// Gets the alternative text for the image (used for accessibility).
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Gets the optional caption displayed below the image.
    /// </summary>
    public string? Caption { get; set; }

    /// <inheritdoc />
    public override string BlockType => "image";

    /// <inheritdoc />
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit((ImageBlock)this);
}

/// <summary>
/// Represents a call-to-action block with a link and optional styling.
/// </summary>
[BlockMetadata("cta", "Call to Action")]
public sealed class CtaBlock : BlockBase
{
    /// <summary>
    /// Gets the display text of the call-to-action.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets the URL target of the call-to-action.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets the optional CSS style class for the call-to-action.
    /// </summary>
    public string? Style { get; set; }

    /// <inheritdoc />
    public override string BlockType => "cta";

    /// <inheritdoc />
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit((CtaBlock)this);
}

/// <summary>
/// Represents a blockquote with optional author and citation.
/// </summary>
[BlockMetadata("quote", "Quote")]
public sealed class QuoteBlock : BlockBase
{
    /// <summary>
    /// Gets the text content of the quote.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets the author of the quote (optional).
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets the citation or source of the quote (optional).
    /// </summary>
    public string? Citation { get; set; }

    /// <inheritdoc />
    public override string BlockType => "quote";

    /// <inheritdoc />
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit((QuoteBlock)this);
}

/// <summary>
/// Represents an embed block for external content (videos, social posts, etc.).
/// </summary>
[BlockMetadata("embed", "Embed")]
public sealed class EmbedBlock : BlockBase
{
    /// <summary>
    /// Gets the type of embedded content (e.g., "youtube", "vimeo", "twitter").
    /// </summary>
    public string EmbedType { get; set; } = string.Empty;

    /// <summary>
    /// Gets the source URL of the external content.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets the optional thumbnail URL for the embedded content.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <inheritdoc />
    public override string BlockType => "embed";

    /// <inheritdoc />
    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit((EmbedBlock)this);
}
