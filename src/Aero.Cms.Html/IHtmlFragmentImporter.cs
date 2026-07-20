using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Validates and imports caller-supplied static HTML into the catalog-backed page-content model.
/// </summary>
/// <remarks>
/// The fragment is untrusted input. Implementations must enforce the supported element,
/// attribute, URL, nesting, and resource policies before returning page content.
/// </remarks>
public interface IHtmlFragmentImporter
{
    /// <summary>
    /// Parses a complete HTML fragment or returns a validation failure without producing partial content.
    /// </summary>
    /// <param name="fragment">Caller-supplied static HTML to parse and validate.</param>
    /// <returns>Fresh page content, or validation errors for unsafe, unsupported, recovered, or over-limit input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fragment"/> is <see langword="null"/>.</exception>
    Result<HtmlPageContent> Import(string fragment);
}
