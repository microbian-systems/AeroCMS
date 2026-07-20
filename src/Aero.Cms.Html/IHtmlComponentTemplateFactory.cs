using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Creates fresh HTML subtrees for curated page-building components.
/// </summary>
public interface IHtmlComponentTemplateFactory
{
    /// <summary>
    /// Creates a disconnected component subtree with fresh stable identities.
    /// </summary>
    /// <param name="kind">The curated component to construct.</param>
    /// <returns>The component root, or a validation failure for an unsupported enum value.</returns>
    Result<HtmlNode> Create(HtmlComponentTemplateKind kind);
}
