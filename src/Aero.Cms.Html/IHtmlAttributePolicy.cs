namespace Aero.Cms.Html;

/// <summary>
/// Determines which attributes are safe and supported for a catalog element.
/// </summary>
public interface IHtmlAttributePolicy
{
    HtmlAttributePolicyDecision CanRender(HtmlElementDefinition element, string attributeName, string attributeValue);
}
