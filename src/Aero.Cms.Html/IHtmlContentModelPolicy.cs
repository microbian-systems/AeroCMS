namespace Aero.Cms.Html;

/// <summary>
/// Determines whether a proposed parent/child relationship is valid for the supported HTML catalog.
/// </summary>
public interface IHtmlContentModelPolicy
{
    HtmlContentPolicyDecision CanContain(HtmlNode parent, HtmlNode child);
}
