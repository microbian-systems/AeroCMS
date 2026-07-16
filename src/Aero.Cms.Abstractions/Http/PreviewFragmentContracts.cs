using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Html;

namespace Aero.Cms.Abstractions.Http;

/// <summary>
/// Request payload for rendering an unsaved page preview fragment.
/// </summary>
public sealed record PreviewPageFragmentRequest(
    HtmlPageContent Content);

/// <summary>
/// Response payload for a rendered page preview fragment.
/// </summary>
public sealed record PreviewPageFragmentResponse(string Html);

/// <summary>
/// Request payload for rendering an unsaved blog post preview fragment.
/// </summary>
public sealed record PreviewBlogPostFragmentRequest(string? MarkdownContent = null);

/// <summary>
/// Response payload for a rendered blog post preview fragment.
/// </summary>
public sealed record PreviewBlogPostFragmentResponse(string Html);

/// <summary>
/// Request payload for rendering an unsaved single block preview fragment.
/// </summary>
public sealed record PreviewBlockFragmentRequest(BlockBase? Block);

/// <summary>
/// Response payload for a rendered single block preview fragment.
/// </summary>
public sealed record PreviewBlockFragmentResponse(string Html);

/// <summary>
/// Response wrapper for preview content.
/// </summary>
/// <param name="Content">The content document being previewed.</param>
/// <param name="ContentType">The type of content (e.g. page, blog-post).</param>
public record PreviewResponse<T>(T Content, string ContentType) where T : class
{
    /// <summary>
    /// Indicates whether the content is a draft. Preview endpoints always serve draft content.
    /// </summary>
    public bool IsDraft => true;
}
