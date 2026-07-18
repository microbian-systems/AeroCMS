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
    Result<HtmlPageContent> Import(string markdown);

    /// <summary>
    /// Converts the losslessly representable subset of a page-content tree to Markdown.
    /// </summary>
    Result<string> Export(HtmlPageContent content);
}
