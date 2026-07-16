using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Imports a trusted-authoring HTML fragment into the catalog-backed page-content model.
/// </summary>
public interface IHtmlFragmentImporter
{
    /// <summary>
    /// Parses a complete HTML fragment or returns a validation failure without producing partial content.
    /// </summary>
    Result<HtmlPageContent> Import(string fragment);
}
