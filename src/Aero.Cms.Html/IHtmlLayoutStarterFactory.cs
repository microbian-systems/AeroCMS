using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Factory for editable layout-template subtrees.
/// </summary>
public interface IHtmlLayoutStarterFactory
{
    /// <summary>
    /// Creates a disconnected layout subtree with fresh stable identities.
    /// </summary>
    /// <param name="kind">The guided layout to construct.</param>
    /// <returns>The layout root, or a validation failure for an unsupported enum value.</returns>
    Result<HtmlNode> Create(HtmlLayoutStarterKind kind);
}
