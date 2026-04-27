using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A block that renders content from Markdown text.
/// </summary>
[BlockMetadata("markdown", "Markdown Text", Category = "Text")]
public sealed class MarkdownBlock : BlockBase
{
    public override string BlockType => "markdown";

    /// <summary>
    /// Gets or sets the raw markdown content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
