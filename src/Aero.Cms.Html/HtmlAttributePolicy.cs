namespace Aero.Cms.Html;

/// <summary>
/// First-release attribute allow-list for public HTML rendering.
/// CSS styling is intentionally modeled separately and raw inline styles are not accepted.
/// </summary>
public sealed class HtmlAttributePolicy : IHtmlAttributePolicy
{
    public HtmlAttributePolicyDecision CanRender(
        HtmlElementDefinition element,
        string attributeName,
        string attributeValue)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrWhiteSpace(attributeName) || !IsValidAttributeName(attributeName))
        {
            return HtmlAttributePolicyDecision.Deny("The attribute name is invalid.");
        }

        if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return HtmlAttributePolicyDecision.Deny("Inline event-handler attributes are not supported.");
        }

        if (string.Equals(attributeName, "style", StringComparison.OrdinalIgnoreCase))
        {
            return HtmlAttributePolicyDecision.Deny("Inline style attributes are not supported; use the style model instead.");
        }

        if (IsGlobalAttribute(attributeName) || IsElementAttribute(element.Tag, attributeName))
        {
            return IsUrlAttribute(attributeName) && !IsSafeUrl(attributeName, attributeValue)
                ? HtmlAttributePolicyDecision.Deny($"The {attributeName} URL scheme is not supported.")
                : HtmlAttributePolicyDecision.Allow();
        }

        return HtmlAttributePolicyDecision.Deny($"The {attributeName} attribute is not supported on <{element.Tag}>.");
    }

    private static bool IsGlobalAttribute(string attributeName) =>
        attributeName.Equals("id", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("class", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("title", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("role", StringComparison.OrdinalIgnoreCase)
        || attributeName.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)
        || attributeName.StartsWith("data-", StringComparison.OrdinalIgnoreCase);

    private static bool IsElementAttribute(string tagName, string attributeName) =>
        (tagName.Equals("a", StringComparison.OrdinalIgnoreCase)
            && (attributeName.Equals("href", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("target", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("rel", StringComparison.OrdinalIgnoreCase)))
        || (tagName.Equals("img", StringComparison.OrdinalIgnoreCase)
            && (attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("alt", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("width", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("height", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("loading", StringComparison.OrdinalIgnoreCase)))
        || (tagName.Equals("button", StringComparison.OrdinalIgnoreCase)
            && (attributeName.Equals("type", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("disabled", StringComparison.OrdinalIgnoreCase)));

    private static bool IsUrlAttribute(string attributeName) =>
        attributeName.Equals("href", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("src", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeUrl(string attributeName, string value) =>
        attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
            ? HtmlUrlPolicy.IsSafeMediaUrl(value)
            : HtmlUrlPolicy.IsSafeNavigationUrl(value);

    private static bool IsValidAttributeName(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not ':')
            {
                return false;
            }
        }

        return char.IsAsciiLetter(value[0]);
    }
}
