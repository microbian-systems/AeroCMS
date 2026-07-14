using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Creates fresh HTML subtrees for curated page-building components.
/// </summary>
public interface IHtmlComponentTemplateFactory
{
    Result<HtmlNode> Create(HtmlComponentTemplateKind kind);
}
