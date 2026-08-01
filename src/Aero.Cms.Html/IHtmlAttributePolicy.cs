namespace Aero.Cms.Html;

/// <summary>
/// Determines which attributes are safe and supported for a catalog element.
/// </summary>
public interface IHtmlAttributePolicy
{
    /// <summary>
    /// Evaluates an attribute immediately before it crosses the static-rendering trust boundary.
    /// </summary>
    /// <param name="element">The catalog definition for the rendered element.</param>
    /// <param name="attributeName">The candidate attribute name.</param>
    /// <param name="attributeValue">The candidate unencoded attribute value.</param>
    /// <returns>An allow decision, or a denial with a reason suitable for validation diagnostics.</returns>
    HtmlAttributePolicyDecision CanRender(HtmlElementDefinition element, string attributeName, string attributeValue);
}
