using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Converts Markdown interchange text to and from the canonical HTML page tree.
/// Markdown is never a persisted page-content format.
/// </summary>
public interface IMarkdownInterchangeAdapter
{
    /// <summary>
    /// Converts Markdown into a fully validated page-content fragment.
    /// </summary>
    /// <param name="markdown">The non-empty Markdown interchange source.</param>
    /// <returns>Fresh canonical page content, or validation errors for unsafe, unsupported, or over-limit input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    Result<HtmlPageContent> Import(string markdown);

    /// <summary>
    /// Converts the losslessly representable subset of a page-content tree to Markdown.
    /// </summary>
    /// <param name="content">The canonical page content to validate and export.</param>
    /// <returns>Markdown that round-trips to equivalent content, or a validation failure without partial output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    Result<string> Export(HtmlPageContent content);
}
