namespace Aero.Cms.Html;

/// <summary>
/// Determines whether a proposed parent/child relationship is valid for the supported HTML catalog.
/// </summary>
public interface IHtmlContentModelPolicy
{
    /// <summary>
    /// Evaluates a direct parent-child relationship against catalog and focused HTML nesting rules.
    /// </summary>
    /// <param name="parent">The proposed direct parent.</param>
    /// <param name="child">The proposed direct child.</param>
    /// <returns>An allow decision, or a denial describing the violated rule.</returns>
    HtmlContentPolicyDecision CanContain(HtmlNode parent, HtmlNode child);
}
