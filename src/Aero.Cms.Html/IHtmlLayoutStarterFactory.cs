using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Factory for editable layout-template subtrees.
/// </summary>
public interface IHtmlLayoutStarterFactory
{
    Result<HtmlNode> Create(HtmlLayoutStarterKind kind);
}
